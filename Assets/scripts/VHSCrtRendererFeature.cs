using UnityEngine;
using UnityEngine.Rendering.Universal;

public class VHSCrtRendererFeature : FullScreenPassRendererFeature
{
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType != CameraType.Game)
        {
            return;
        }

        base.AddRenderPasses(renderer, ref renderingData);
    }
}
