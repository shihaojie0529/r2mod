using UnityEngine;
namespace RiftSurvivor
{
    internal sealed class PortalFramePulse : MonoBehaviour
    {
        private LineRenderer[] lines;
        private Color[] colors;
        private float began;
        private void Start()
        {
            began=Time.time; lines=GetComponentsInChildren<LineRenderer>();
            colors=new Color[lines.Length];
            for(int i=0;i<lines.Length;i++) colors[i]=lines[i].startColor;
        }
        private void Update()
        {
            float age=Time.time-began;
            float envelope=Mathf.SmoothStep(0f,1f,age/.14f) * (1f-Mathf.SmoothStep(0f,1f,(age-2.7f)/.3f));
            for(int i=0;i<lines.Length;i++)
            {
                var color=colors[i]; color.a*=envelope*(.84f+.16f*Mathf.Sin(age*3f+i*.4f));
                lines[i].startColor=lines[i].endColor=color;
            }
        }
    }
}
