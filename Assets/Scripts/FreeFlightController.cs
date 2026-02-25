using Oculus.Interaction.Locomotion;
using UnityEngine;
using OculusCC = Oculus.Interaction.Locomotion.CharacterController;

public class FreeFlightController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Arrastra aquí tu Main Camera o CenterEyeAnchor")]
    public Transform cameraTransform;

    [Header("Movimiento")]
    [Tooltip("Velocidad de desplazamiento en suelo")]
    public float groundSpeed = 3.0f;

    [Tooltip("Velocidad de vuelo")]
    public float flySpeed = 5.0f;

    [Header("Gravedad")]
    [Tooltip("Fuerza de gravedad (se aplica en suelo y al desactivar vuelo)")]
    public float gravity = -9.81f;

    [Header("Vuelo")]
    [Tooltip("Metros que sube al activar el modo vuelo")]
    public float takeoffBoost = 2.0f;

    [Tooltip("Referencia al CharacterController de Oculus Locomotion")]
    public OculusCC characterController;

    [Tooltip("Transform raíz del Camera Rig (se autodetecta si no se asigna)")]
    public Transform playerRig;

    // Componentes que hay que desactivar durante el vuelo
    private FirstPersonLocomotor _fpLocomotor;
    private LocomotionAxisTurnerInteractor[] _turners;

    private float verticalVelocity;
    private bool isFlying = false;

    void Start()
    {
        // Buscar el CharacterController de Oculus si no se asignó
        if (characterController == null)
            characterController = GetComponent<OculusCC>();
        if (characterController == null)
            characterController = GetComponentInChildren<OculusCC>();
        if (characterController == null)
            characterController = GetComponentInParent<OculusCC>();

        if (characterController == null)
            Debug.LogError("[FreeFlightController] No se encontró Oculus CharacterController!");
        else
            Debug.Log("[FreeFlightController] CharacterController encontrado en: " + characterController.gameObject.name);

        // Buscar el locomotor que aplica gravedad
        _fpLocomotor = FindObjectOfType<FirstPersonLocomotor>();
        if (_fpLocomotor == null)
            Debug.LogWarning("[FreeFlightController] No se encontró FirstPersonLocomotor");

        // Buscar snap turn interactors (consumen el joystick derecho)
        _turners = FindObjectsOfType<LocomotionAxisTurnerInteractor>();

        if (cameraTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
                cameraTransform = mainCam.transform;
            else
                cameraTransform = FindObjectOfType<Camera>().transform;
        }

        // Autodetectar el rig (player origin): subimos desde el CC hasta la raíz
        if (playerRig == null && characterController != null)
        {
            // El rig suele ser el padre del CC, o el propio transform si el script está en el rig
            playerRig = characterController.transform.parent;
            if (playerRig == null)
                playerRig = characterController.transform;
        }

        if (playerRig != null)
            Debug.Log("[FreeFlightController] Player Rig: " + playerRig.gameObject.name);
        else
            Debug.LogError("[FreeFlightController] No se encontró el Player Rig!");

        // Colocar al personaje sobre el terreno al iniciar
        SnapToGround();
    }

    /// <summary>
    /// Coloca el CharacterController justo encima del terreno usando SetPosition + TryGround.
    /// </summary>
    private void SnapToGround()
    {
        if (characterController == null) return;

        // Lanzar raycast para encontrar el suelo
        Vector3 origin = characterController.transform.position + Vector3.up * 200f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 500f))
        {
            float halfHeight = characterController.Height * 0.5f + characterController.SkinWidth;
            Vector3 targetPos = hit.point + Vector3.up * halfHeight;

            // Usar SetPosition (teleport sin colisión) y luego TryGround para ajustar
            characterController.SetPosition(targetPos);
            characterController.TryGround();

            Debug.Log("[FreeFlightController] SnapToGround: posicionado en " + targetPos + " (terreno en " + hit.point + ")");
        }
        else
        {
            Debug.LogWarning("[FreeFlightController] SnapToGround: no se encontró suelo debajo!");
        }

        verticalVelocity = 0f;
    }

    void Update()
    {
        // --- Toggle modo vuelo con botón X del mando izquierdo ---
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch))
        {
            isFlying = !isFlying;

            if (isFlying)
            {
                // ACTIVAR VUELO
                // 1. Desactivar el locomotor COMPLETAMENTE (para gravedad, grounding y CatchUp)
                if (_fpLocomotor != null)
                    _fpLocomotor.enabled = false;

                // 2. Desactivar snap turn (libera el joystick derecho)
                foreach (var t in _turners)
                    if (t != null) t.enabled = false;

                // 3. Subir el rig para despegar
                verticalVelocity = 0f;
                if (playerRig != null)
                    playerRig.position += Vector3.up * takeoffBoost;
            }
            else
            {
                // DESACTIVAR VUELO
                verticalVelocity = 0f;

                // 1. Reactivar snap turn
                foreach (var t in _turners)
                    if (t != null) t.enabled = true;

                // 2. Reactivar el locomotor completamente
                if (_fpLocomotor != null)
                {
                    _fpLocomotor.enabled = true;
                    _fpLocomotor.EnableMovement();
                }
            }

            Debug.Log("[FreeFlightController] Modo vuelo: " + (isFlying ? "ACTIVADO" : "DESACTIVADO"));
        }

        if (isFlying)
        {
            // En vuelo: joystick IZQUIERDO para volar
            Vector2 flyAxis = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            HandleFlying(flyAxis);
        }
        else
        {
            // En suelo: joystick IZQUIERDO para caminar
            Vector2 walkAxis = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            HandleGround(walkAxis);
        }
    }

    private void HandleFlying(Vector2 inputAxis)
    {
        if (playerRig == null) return;

        // MODO VUELO: movimiento 3D completo, sin gravedad.
        // Movemos directamente el rig (player origin) ya que el locomotor está desactivado.
        if (inputAxis.magnitude > 0.1f)
        {
            Transform cam = Camera.main != null ? Camera.main.transform : cameraTransform;

            Vector3 forward = cam.forward;
            Vector3 right = cam.right;

            Vector3 moveDirection = (forward * inputAxis.y + right * inputAxis.x).normalized;
            Vector3 movement = moveDirection * flySpeed * Time.deltaTime;

            playerRig.position += movement;
        }
        // Sin input en vuelo: flotar en sitio
    }

    private void HandleGround(Vector2 inputAxis)
    {
        if (characterController == null) return;

        bool grounded = characterController.IsGrounded;

        /**Vector3 move = Vector3.zero;

        if (grounded)
        {
            // Resetear velocidad vertical al tocar suelo
            if (verticalVelocity < 0f)
                verticalVelocity = -2f; // empuje pequeño para mantener contacto
        }

        // Movimiento horizontal (siempre, en suelo o cayendo)
        if (inputAxis.magnitude > 0.1f)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDirection = (forward * inputAxis.y + right * inputAxis.x).normalized;
            move = moveDirection * groundSpeed * Time.deltaTime;
        }

        // Gravedad
        verticalVelocity += gravity * Time.deltaTime;
        verticalVelocity = Mathf.Max(verticalVelocity, -20f); // clamp caída

        move.y = verticalVelocity * Time.deltaTime;

        characterController.Move(move);**/
    }
}