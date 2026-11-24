"""Paint team cloth and heraldry into the existing Castle Guard UV atlas."""
from pathlib import Path

import bpy
import numpy as np


folder = Path("Assets/Art/Textures/Characters/CastleGuard").resolve()
original = bpy.data.images.load(str(folder / "T_CastleGuard_BaseColor.png"), check_existing=False)
width, height = original.size
rgba = np.empty(width * height * 4, dtype=np.float32)
original.pixels.foreach_get(rgba)
rgba = rgba.reshape(height, width, 4)
mesh = bpy.data.objects["CastleGuard_Armor"].data
mesh.calc_loop_triangles()
uvs = mesh.uv_layers.active.data
cloth = np.zeros((height, width), dtype=bool)
badge = cloth.copy()
team_a = cloth.copy()
team_b = cloth.copy()

for triangle in mesh.loop_triangles:
    material = mesh.materials[mesh.polygons[triangle.polygon_index].material_index].name
    if material not in {"Guard_Oxblood", "Guard_AntiqueBrass"}:
        continue
    uv = np.array([uvs[index].uv[:] for index in triangle.loops]) * (width, height)
    low = np.maximum(np.floor(uv.min(axis=0)).astype(int), 0)
    high = np.minimum(np.ceil(uv.max(axis=0)).astype(int), (width, height))
    if np.any(high <= low):
        continue
    x, y = np.meshgrid(np.arange(low[0], high[0]) + .5, np.arange(low[1], high[1]) + .5)
    xy = np.stack((x, y), axis=-1) - uv[0]
    basis = (uv[1:] - uv[0]).T
    if abs(np.linalg.det(basis)) < 1e-8:
        continue
    b, c = (xy @ np.linalg.inv(basis).T).transpose(2, 0, 1)
    a = 1 - b - c
    inside = (a >= 0) & (b >= 0) & (c >= 0)
    p0, p1, p2 = (np.array(mesh.vertices[index].co[:]) for index in triangle.vertices)
    px, py, pz = (a[..., None] * p0 + b[..., None] * p1 + c[..., None] * p2).transpose(2, 0, 1)
    region = np.s_[low[1]:high[1], low[0]:high[0]]
    if material == "Guard_AntiqueBrass":
        chest = inside & (py > .188) & (abs(px) < .052) & (pz > 1.253) & (pz < 1.314)
        badge[region] |= chest
        team_a[region] |= chest & (abs(px) > .005) & (abs(pz - (1.304 - .69 * abs(px))) < .008)
        team_b[region] |= chest & (np.minimum(np.minimum(abs(px - .031), abs(px + .031)), abs(px)) < .006)
        continue
    cloth[region] |= inside

    # Put the front sign on the tabard below the common metal garrison badge.
    # The wider rear mantle uses the same sign at chest height.
    rear = py < 0
    sign_x = np.where(rear, px, px * 2)
    sign_z = np.where(rear, pz - 1.27, (pz - .88) * 1.4)
    sign_field = (abs(sign_x) < .103) & (abs(sign_z) < .09)
    forked_chevron = (abs(sign_x) > .012) & (abs(sign_z - (.045 - .82 * abs(sign_x))) < .018)
    stripes = (np.minimum(np.minimum(abs(sign_x - .062), abs(sign_x + .062)), abs(sign_x)) < .014)
    team_a[region] |= inside & sign_field & forked_chevron
    team_b[region] |= inside & sign_field & stripes

if min(cloth.sum(), team_a.sum(), team_b.sum()) == 0:
    raise ValueError("Team cloth or heraldry failed to map to the shared UV atlas.")

for team, color, crest in (
    ("TeamA", (.45, .80, 1.55), team_a),
    ("TeamB", (1.26, .41, .49), team_b),
):
    pixels = rgba.copy()
    color_detail = rgba[cloth, :3].max(axis=1)
    pixels[cloth, :3] = np.minimum(color_detail[:, None] * color, 1)
    badge_detail = rgba[badge, :3].max(axis=1)
    pixels[badge, :3] = np.minimum(.60 * badge_detail[:, None] * color, 1)
    pixels[crest, :3] = np.clip(.36 + .76 * pixels[crest, :3], 0, 1)
    original.pixels.foreach_set(pixels.flatten())
    original.filepath_raw = str(folder / f"T_CastleGuard_{team}_BaseColor.png")
    original.file_format = "PNG"
    original.save()
    print(f"Saved {team} atlas with {crest.sum()} heraldry pixels", flush=True)
