#if FUSION_WEAVER && FUSION_WEAVER_ILPOSTPROCESSOR
namespace Fusion.CodeGen {
  partial class ILWeaverSettings {
    static partial void OverrideNetworkProjectConfigPath(ref string path) {
      if (System.IO.Directory.Exists("Assets/ThirdParty/Photon"))
        path = "Assets/ThirdParty/Photon/Fusion/Resources/NetworkProjectConfig.fusion";
    }
  }
}
#endif
