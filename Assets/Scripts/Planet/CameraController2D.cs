using UnityEngine;
using UnityEngine.InputSystem;
using MercurioIdle.Robots;

namespace MercurioIdle.Chunks
{
    /// <summary>
    /// Controla a câmera 2D para explorar o mapa (e fazer os chunks carregarem):
    ///  - segue um minion (começa seguindo o primeiro que existir);
    ///  - WASD / setas: move a câmera livremente (solta o minion);
    ///  - F: volta a seguir o minion;
    ///  - Tab: troca para o próximo minion.
    /// Coloque este script na Main Camera.
    /// </summary>
    public sealed class CameraController2D : MonoBehaviour
    {
        [Header("Mover")]
        [SerializeField, Tooltip("Velocidade do WASD / setas (m/s).")]
        private float panSpeed = 15f;

        [Header("Seguir minion")]
        [SerializeField, Tooltip("Começa seguindo um minion.")]
        private bool followMinion = true;
        [SerializeField, Tooltip("Suavidade do acompanhamento (s). Menor = mais colado.")]
        private float followSmoothTime = 0.15f;
        [SerializeField, Tooltip("Opcional: alvo fixo. Vazio = primeiro minion da cena.")]
        private Transform target;

        private Vector3 velocity;
        private float searchTimer;

        private void LateUpdate()
        {
            var keyboard = Keyboard.current;
            Vector2 input = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;

                if (keyboard.fKey.wasPressedThisFrame) followMinion = true;
                if (keyboard.tabKey.wasPressedThisFrame)
                {
                    CycleTarget();
                    followMinion = true;
                }
            }

            // Mover à mão solta a câmera do minion
            if (input != Vector2.zero)
            {
                followMinion = false;
                velocity = Vector3.zero;
                transform.position += (Vector3)(input.normalized * (panSpeed * Time.deltaTime));
                return;
            }

            if (!followMinion) return;

            if (target == null)
            {
                searchTimer -= Time.deltaTime;
                if (searchTimer > 0f) return;
                searchTimer = 0.5f;

                var minion = FindFirstObjectByType<Minion>();
                if (minion == null) return;
                target = minion.transform;
            }

            Vector3 goal = new Vector3(target.position.x, target.position.y, transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, followSmoothTime);
        }

        private void CycleTarget()
        {
            var minions = FindObjectsByType<Minion>(FindObjectsSortMode.InstanceID);
            if (minions.Length == 0) return;

            int current = -1;
            for (int i = 0; i < minions.Length; i++)
                if (minions[i].transform == target) current = i;

            target = minions[(current + 1) % minions.Length].transform;
        }
    }
}
