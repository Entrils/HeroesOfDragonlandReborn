"""Bake the Borderwood sky in scene-linear HDR. Run in Blender with -- PROJECT_ROOT."""
import math
import sys
from pathlib import Path

import bpy
import numpy as np


ROOT = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
OUTPUT = ROOT / 'Assets/Art/Textures/Environment/T_BorderwoodNight.exr'
WIDTH, HEIGHT = 4096, 2048
TAU = 2 * math.pi
rng = np.random.default_rng(88243)
lattice = rng.random((64, 64, 64), dtype=np.float32)


def noise(x, y, z):
    """Smooth solid noise keeps longitude seams and body silhouettes continuous."""
    ix, iy, iz = (np.floor(t).astype(np.int32) for t in (x, y, z))
    a, b, c = (t - i for t, i in zip((x, y, z), (ix, iy, iz)))
    a, b, c = (t*t*(3-2*t) for t in (a,b,c))
    result = 0
    for k in (0,1):
        for j in (0,1):
            for i in (0,1):
                result += (a if i else 1-a)*(b if j else 1-b)*(c if k else 1-c)*lattice[
                    (iz+k) % 64, (iy+j) % 64, (ix+i) % 64]
    return result


def turbulence(x, y, z):
    return (.54*noise(x,y,z) + .27*noise(2*x+3,2*y,2*z) +
            .13*noise(4*x,4*y+5,4*z) + .06*noise(8*x,8*y,8*z+7))


def direction(az, el):
    return np.stack((np.sin(az)*np.cos(el), np.sin(el), np.cos(az)*np.cos(el)), axis=-1)


def sky_band(el, az):
    az, el = np.broadcast_arrays(az, el)
    d = direction(az, el)
    x,y,z = d.transpose(2,0,1)
    haze = np.exp(-np.maximum(y,0)*5.4)
    sky = np.array([.0030,.0050,.0135]) + haze[:,:,None]*[.029,.040,.059]
    dust = turbulence(x*6+11,y*6,z*6)
    drift = np.exp(-((.48*x+.76*y-.43*z+.09*(dust-.5))/.18)**2)
    lane = np.maximum(0,dust-.31)**1.45 * drift * np.clip(y*4,0,1)
    sky += lane[:,:,None]*[.066,.061,.099]
    return sky


def stars(sky):
    # Uniform solid-angle placement. Stamp luminous cores and a separate, faint halo.
    # Longitude stretches at the poles, so evaluate both profiles in angular distance.
    for _ in range(12400):
        az = rng.uniform(-math.pi, math.pi)
        el = math.asin(rng.uniform(.018,1))
        power = rng.random()**5
        elevation = min(1, max(0, (math.sin(el)-.025)*7))
        peak = (.080 + 6.6*power**3) * elevation
        sigma = .35+.21*power
        span = 3 if power<.48 else 9
        px = (az/TAU+.5)*WIDTH-.5
        py = (el/math.pi+.5)*HEIGHT-.5
        aspect = max(.002,math.cos(el))
        ix = np.arange(math.floor(px-span/aspect),math.ceil(px+span/aspect)+1)
        iy = np.arange(max(0,math.floor(py-span)),min(HEIGHT,math.ceil(py+span)+1))
        r2 = ((ix-px)*aspect)**2 + (iy[:,None]-py)**2
        core = np.exp(-r2/(2*sigma*sigma))
        halo = .027*power**3*np.exp(-np.sqrt(r2)/1.65)
        if power>.94:
            # A few first-magnitude stars catch narrow glints, tapering into the halo.
            x,y = (ix-px)*aspect,iy[:,None]-py
            halo += .045*(np.exp(-abs(x)*2.8-abs(y)*.54) + np.exp(-abs(y)*3.2-abs(x)*.70))
        tint = rng.choice(((.78,.87,1.0),(.95,.96,1.0),(1.0,.84,.67)),p=(.34,.51,.15))
        sky[iy[:,None], ix % WIDTH, :3] += (peak*(core+halo))[:,:,None]*tint


def body(sky, azimuth, elevation, radius, moon):
    # Work only on the rectangle containing the sphere and its atmospheric glow.
    az, el, r = np.radians([azimuth,elevation,radius])
    reach = r*(3.6 if moon else 1.23)
    px = (az/TAU+.5)*WIDTH
    py = (el/math.pi+.5)*HEIGHT
    w = math.ceil(reach/math.cos(el+reach)*WIDTH/TAU)
    h = math.ceil(reach*HEIGHT/math.pi)
    ix = np.arange(int(px)-w,int(px)+w+1)
    iy = np.arange(int(py)-h,int(py)+h+1)
    lon, lat = np.meshgrid(((ix+.5)/WIDTH-.5)*TAU,((iy+.5)/HEIGHT-.5)*math.pi)
    ray = direction(lon,lat)
    axis = direction(az,el)
    right = np.array([math.cos(az),0,-math.sin(az)])
    up = np.cross(axis,right)
    x, y = (ray @ t / math.sin(r) for t in (right,up))
    rho = np.sqrt(x*x+y*y)
    z = np.sqrt(np.maximum(0,1-rho*rho))
    light = np.clip(-.77*x+.33*y+.52*z,0,1)
    shade = turbulence(x*4+3,y*4+11,z*4)
    if moon:
        # Broad maria plus impact bowls with a directional rim and a recessed floor.
        rock = .52+.70*shade + .10*noise(36*x,36*y,36*z)
        for _ in range(115):
            cx,cy = rng.uniform(-1,1,2)
            cr = rng.uniform(.018,.145)**1.12
            dx,dy = (x-cx)/cr,(y-cy)/cr
            dist = np.sqrt(dx*dx+dy*dy)
            rock += .20*np.exp(-((dist-.89)/.17)**2)*(-dx+.4*dy) - .15*np.exp(-dist**4*3)
        colour = (np.clip(rock,.28,1.35)*(.04+2.35*light**.70))[:,:,None]*[.81,.89,1]
        glow = np.exp(-np.maximum(rho-1,0)*3.1)[:,:,None]*[.070,.090,.15]
    else:
        latitude = .31*x+.94*y-.12*z
        flow = latitude+.18*(shade-.5)+.10*(turbulence(9*x,8*y,6*z)-.5)
        belts = .50+.21*np.sin(24*flow+.9)+.13*np.sin(63*flow)+.07*np.sin(149*flow+shade*5)
        # A tilted anticyclone interrupts the narrow cloud bands on the lit hemisphere.
        storm = np.hypot((x+.36)/.25,(latitude+.13)/.080)
        belts += .18*np.exp(-storm*storm)*np.cos(11*storm+shade*4)
        belts = np.clip(belts+.24*(shade-.5)+.085*(noise(38*x,32*y,38*z)-.5),0,1)
        colour = np.array([.21,.012,.018])+belts[:,:,None]*[.67,.095,.064]
        colour *= (.032+.94*light**.79)[:,:,None]
        rim = (1-z)**4 * (.20+.80*np.clip(-x+.25*y,0,1))
        colour += rim[:,:,None]*[.31,.060,.035]
        glow = np.exp(-np.maximum(rho-1,0)*29)[:,:,None]*[.044,.008,.006]
    patch = sky[iy[0]:iy[-1]+1, ix[0]:ix[-1]+1, :3]
    edge = np.clip(.5+(1-rho)*math.sin(r)*WIDTH/TAU,0,1)[:,:,None]
    patch[:] = (patch+glow)*(1-edge) + colour*edge


sky = np.ones((HEIGHT,WIDTH,4), dtype=np.float32)
az = ((np.arange(WIDTH)+.5)/WIDTH-.5)*TAU
for row in range(0,HEIGHT,64):
    lat = ((np.arange(row,row+64)+.5)/HEIGHT-.5)*math.pi
    sky[row:row+64,:,:3] = sky_band(lat[:,None],az)
stars(sky)
body(sky,108,17,8.8,False)
body(sky,73,24,2.15,True)
assert np.isfinite(sky).all() and sky[:,:,:3].max()>4
image = bpy.data.images.new('Borderwood night radiance',WIDTH,HEIGHT,alpha=False,float_buffer=True)
image.colorspace_settings.name = 'Non-Color'
image.pixels.foreach_set(sky.ravel())
scene = bpy.context.scene
scene.render.image_settings.file_format = 'OPEN_EXR'
scene.render.image_settings.color_mode = 'RGB'
scene.render.image_settings.color_depth = '16'
scene.render.image_settings.exr_codec = 'ZIP'
image.save_render(str(OUTPUT),scene=scene)
print(f'Wrote {OUTPUT.name}, peak linear radiance {sky[:,:,:3].max():.2f}',flush=True)
