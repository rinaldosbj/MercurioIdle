using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Camera))]
public sealed class DysonPerspectiveController : MonoBehaviour
{
    [SerializeField, Tooltip("Simulacao que fornece a posicao exata do foco e do Sol.")]
    private DysonSwarmRenderer swarm;
    [SerializeField, Tooltip("Geometria visual do Sol. Sua escala externa e restaurada ao sair do foco.")]
    private Transform sunVisual;
    [SerializeField, Tooltip("Material do Sol na vista do foco, com cor suave e escurecimento no limbo.")]
    private Material focusSunMaterial;
    [SerializeField, Tooltip("Botao que alterna entre a vista externa e a vista do foco.")]
    private Button perspectiveButton;
    [SerializeField, Tooltip("Texto do botao de perspectiva.")]
    private TMP_Text buttonLabel;
    [SerializeField, Min(0.001f), Tooltip("Raio visual do Sol, em unidades mundiais, na vista do foco. Nao muda os raios orbitais ou a populacao.")]
    private float focusSunRadius = 0.08f;
    [SerializeField, Range(20,110), Tooltip("Campo de visao da camera no foco, em graus.")]
    private float focusFieldOfView = 75;
    [SerializeField, Min(0.01f), Tooltip("Graus de rotacao por pixel ao arrastar com o botao direito do mouse, nas duas perspectivas.")]
    private float lookSensitivity = 0.15f;
    [SerializeField, Min(0.001f), Tooltip("Velocidade do zoom orbital com a roda do mouse.")]
    private float zoomSensitivity = 0.15f;
    [SerializeField, Min(0.01f), Tooltip("Distancia minima da camera externa ao centro do Sol.")]
    private float minimumOrbitDistance = 5f;
    [SerializeField, Min(0.01f), Tooltip("Distancia maxima da camera externa ao centro do Sol.")]
    private float maximumOrbitDistance = 200f;
    private bool dragging;
    private Camera viewCamera;
    private Renderer sunRenderer;
    private Vector3 outsidePosition, outsideScale;
    private Quaternion outsideRotation;
    private Material[] outsideMaterials;
    private float outsideFov, outsideNear, outsideOrthographicSize, sunLocalRadius;
    private bool outsideOrthographic, captured;
    public bool IsFocusView { get; private set; }

    private void OnEnable()
    {
        if (Application.isPlaying) {
            CaptureOutside();
            if (perspectiveButton != null) perspectiveButton.onClick.AddListener(TogglePerspective);
            UpdateLabel();
        }
    }
    private void CaptureOutside()
    {
        if(captured) return;
        viewCamera=GetComponent<Camera>();
        outsidePosition=transform.position;outsideRotation=transform.rotation;
        outsideFov=viewCamera.fieldOfView;outsideNear=viewCamera.nearClipPlane;
        outsideOrthographic=viewCamera.orthographic;outsideOrthographicSize=viewCamera.orthographicSize;
        if(sunVisual!=null) {
            outsideScale=sunVisual.localScale;sunRenderer=sunVisual.GetComponent<Renderer>();
            if(sunRenderer!=null) outsideMaterials=sunRenderer.sharedMaterials;
            var mesh=sunVisual.GetComponent<MeshFilter>();
            sunLocalRadius=mesh!=null && mesh.sharedMesh!=null ? mesh.sharedMesh.bounds.extents.x : 0.5f;
        }
        captured=true;
    }
    public void TogglePerspective() { SetFocusView(!IsFocusView); }
    public void SetFocusView(bool focus)
    {
        CaptureOutside();
        if(swarm==null || viewCamera==null) return;
        if(focus == IsFocusView) return;
        dragging=false;
        if(focus) {
            outsidePosition=transform.position;
            outsideRotation=transform.rotation;
            outsideOrthographicSize=viewCamera.orthographicSize;
        }
        IsFocusView=focus;
        if(focus) {
            transform.position=swarm.FocusPosition;
            Vector3 direction=swarm.SunPosition-transform.position;
            if(direction.sqrMagnitude>0.000001f) transform.rotation=Quaternion.LookRotation(direction,Mathf.Abs(Vector3.Dot(direction.normalized,Vector3.up))>0.99f ? Vector3.forward : Vector3.up);
            viewCamera.orthographic=false;viewCamera.fieldOfView=focusFieldOfView;
            viewCamera.nearClipPlane=Mathf.Min(outsideNear,0.02f);
            if(sunVisual!=null) {
                // Preserve relative scale, changing only the visible star geometry.
                float outsideRadius=sunLocalRadius*Mathf.Max(Mathf.Abs(outsideScale.x),Mathf.Abs(outsideScale.y),Mathf.Abs(outsideScale.z));
                sunVisual.localScale=outsideScale*(focusSunRadius/Mathf.Max(outsideRadius,0.0001f));
            }
            if(sunRenderer!=null && focusSunMaterial!=null) sunRenderer.sharedMaterial=focusSunMaterial;
            swarm.SetViewSolarRadius(focusSunRadius);
        } else {
            transform.SetPositionAndRotation(outsidePosition,outsideRotation);
            viewCamera.orthographic=outsideOrthographic;viewCamera.orthographicSize=outsideOrthographicSize;
            viewCamera.fieldOfView=outsideFov;viewCamera.nearClipPlane=outsideNear;
            if(sunVisual!=null) sunVisual.localScale=outsideScale;
            if(sunRenderer!=null && outsideMaterials!=null) sunRenderer.sharedMaterials=outsideMaterials;
            swarm.SetViewSolarRadius(0);
        }
        UpdateLabel();
    }
    private void LateUpdate()
    {
        if(!Application.isPlaying || !captured || swarm==null) return;
        if(IsFocusView) transform.position=swarm.FocusPosition;
        var mouse=Mouse.current;
        if(mouse==null) return;
        bool overUI=EventSystem.current!=null && EventSystem.current.IsPointerOverGameObject();
        if(mouse.rightButton.wasPressedThisFrame && !overUI) dragging=true;
        if(!mouse.rightButton.isPressed || !Application.isFocused) dragging=false;
        ApplyCameraInput(dragging ? mouse.delta.ReadValue() : Vector2.zero,
            overUI ? 0f : mouse.scroll.ReadValue().y / 120f);
    }
    public void ApplyCameraInput(Vector2 lookDelta, float scrollSteps)
    {
        if(!captured || swarm==null) return;
        if(IsFocusView) {
            transform.position=swarm.FocusPosition;
            Vector3 angles=transform.eulerAngles;
            float pitch=Mathf.DeltaAngle(0,angles.x);
            transform.rotation=Quaternion.Euler(Mathf.Clamp(pitch-lookDelta.y*lookSensitivity,-89f,89f),
                angles.y+lookDelta.x*lookSensitivity,0);
            return;
        }
        Vector3 offset=transform.position-swarm.SunPosition;
        float distance=Mathf.Max(offset.magnitude,0.001f);
        float yaw=Mathf.Atan2(offset.x,offset.z)*Mathf.Rad2Deg;
        float elevation=Mathf.Asin(Mathf.Clamp(offset.y/distance,-1f,1f))*Mathf.Rad2Deg;
        yaw+=lookDelta.x*lookSensitivity;
        elevation=Mathf.Clamp(elevation-lookDelta.y*lookSensitivity,-89f,89f);
        if(viewCamera.orthographic) {
            viewCamera.orthographicSize=Mathf.Clamp(viewCamera.orthographicSize*Mathf.Exp(-scrollSteps*zoomSensitivity),
                minimumOrbitDistance,Mathf.Max(minimumOrbitDistance,maximumOrbitDistance));
        } else if(scrollSteps!=0) {
            distance=Mathf.Clamp(distance*Mathf.Exp(-scrollSteps*zoomSensitivity),minimumOrbitDistance,
                Mathf.Max(minimumOrbitDistance,maximumOrbitDistance));
        }
        if(lookDelta.sqrMagnitude==0 && scrollSteps==0) return;
        offset=Quaternion.Euler(-elevation,yaw,0)*Vector3.forward*distance;
        transform.position=swarm.SunPosition+offset;
        transform.rotation=Quaternion.LookRotation(-offset,Vector3.up);
    }
    private void OnApplicationFocus(bool focused) { if(!focused) dragging=false; }
    private void UpdateLabel() { if(buttonLabel!=null) buttonLabel.text=IsFocusView ? "Ver de fora" : "Ver do foco"; }
    private void OnDisable()
    {
        if(perspectiveButton!=null) perspectiveButton.onClick.RemoveListener(TogglePerspective);
        if(captured && IsFocusView) SetFocusView(false);
        captured=false;
    }
}
