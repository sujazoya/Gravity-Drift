using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


namespace zoya.game
{



    public class SpectatorCamera : NetworkBehaviour
    {
        [Header("Camera Settings")]
        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float fastMoveSpeed = 25f;
        [SerializeField] private float lookSpeed = 2f;
        [SerializeField] private float zoomSpeed = 10f;
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 50f;

        [Header("Spectator Targets")]
        [SerializeField] private LayerMask playerLayerMask;
        [SerializeField] private float targetSwitchCooldown = 1f;

        private Camera _spectatorCamera;
        private Vector2 _lookInput;
        private Vector3 _moveInput;
        private bool _isFastMoving;
        private float _currentZoom;
        private Transform _currentTarget;
        private int _currentTargetIndex;
        private List<NetworkObject> _spectatablePlayers = new List<NetworkObject>();
        private float _lastTargetSwitchTime;

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _fastMoveAction;
        private InputAction _zoomAction;
        private InputAction _switchTargetAction;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (IsServerInitialized)
            {
                InitializeSpectatorSystem();
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!base.IsOwner)
            {
                enabled = false;
                return;
            }

            _spectatorCamera = GetComponent<Camera>();
            _currentZoom = _spectatorCamera.orthographic ? _spectatorCamera.orthographicSize : _spectatorCamera.fieldOfView;

            SetupInput();
            FindSpectatablePlayers();

            if (_spectatablePlayers.Count > 0)
            {
                SetTarget(_spectatablePlayers[0].transform);
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void InitializeSpectatorSystem()
        {
            InstanceFinder.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
        }

        private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                RemovePlayerFromSpectatableList(conn);
            }
        }

        [Server]
        private void RemovePlayerFromSpectatableList(NetworkConnection conn)
        {
            NetworkObject playerObj = conn.FirstObject;
            if (playerObj != null && _spectatablePlayers.Contains(playerObj))
            {
                _spectatablePlayers.Remove(playerObj);
            }
        }

        private void SetupInput()
        {
            _moveAction = new InputAction("Move", InputActionType.Value, "<Keyboard>/wasd");
            _lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            _fastMoveAction = new InputAction("FastMove", InputActionType.Button, "<Keyboard>/leftShift");
            _zoomAction = new InputAction("Zoom", InputActionType.Value, "<Mouse>/scroll");
            _switchTargetAction = new InputAction("SwitchTarget", InputActionType.Button, "<Keyboard>/tab");

            _moveAction.Enable();
            _lookAction.Enable();
            _fastMoveAction.Enable();
            _zoomAction.Enable();
            _switchTargetAction.Enable();
        }

        private void Update()
        {
            if (!base.IsOwner) return;

            HandleInput();
            HandleMovement();
            HandleLooking();
            HandleZoom();
            HandleTargetSwitching();
        }

        private void HandleInput()
        {
            Vector2 move = _moveAction.ReadValue<Vector2>();
            _moveInput = new Vector3(move.x, 0, move.y);
            _lookInput = _lookAction.ReadValue<Vector2>();
            _isFastMoving = _fastMoveAction.ReadValue<float>() > 0.5f;
        }

        private void HandleMovement()
        {
            float speed = _isFastMoving ? fastMoveSpeed : moveSpeed;
            Vector3 move = transform.forward * _moveInput.z + transform.right * _moveInput.x;
            transform.position += move * speed * Time.deltaTime;
        }

        private void HandleLooking()
        {
            if (_lookInput.magnitude > 0.1f)
            {
                Vector3 rotation = transform.eulerAngles;
                rotation.x -= _lookInput.y * lookSpeed * Time.deltaTime;
                rotation.y += _lookInput.x * lookSpeed * Time.deltaTime;
                rotation.x = Mathf.Clamp(rotation.x > 180 ? rotation.x - 360 : rotation.x, -89, 89);
                transform.eulerAngles = rotation;
            }
        }

        private void HandleZoom()
        {
            float zoom = _zoomAction.ReadValue<Vector2>().y;
            if (Mathf.Abs(zoom) > 0.1f)
            {
                _currentZoom -= zoom * zoomSpeed * Time.deltaTime;
                _currentZoom = Mathf.Clamp(_currentZoom, minZoom, maxZoom);

                if (_spectatorCamera.orthographic)
                {
                    _spectatorCamera.orthographicSize = _currentZoom;
                }
                else
                {
                    _spectatorCamera.fieldOfView = _currentZoom;
                }
            }
        }

        private void HandleTargetSwitching()
        {
            if (_switchTargetAction.triggered && Time.time - _lastTargetSwitchTime > targetSwitchCooldown)
            {
                SwitchToNextTarget();
                _lastTargetSwitchTime = Time.time;
            }
        }
        [Client]
        private void FindSpectatablePlayers()
        {
            _spectatablePlayers.Clear();

            // Use ClientManager.Objects to get spawned objects on client
            foreach (NetworkObject playerObj in InstanceFinder.ClientManager.Objects.Spawned.Values)
            {
                if (playerObj == null || playerObj == base.NetworkObject)
                    continue;

                // Check if object is on the correct layer from the mask
                int objLayer = playerObj.gameObject.layer;
                if (((1 << objLayer) & playerLayerMask.value) != 0)
                {
                    _spectatablePlayers.Add(playerObj);
                }
            }
        }


        [Client]
        private void SwitchToNextTarget()
        {
            if (_spectatablePlayers.Count == 0) return;

            _currentTargetIndex = (_currentTargetIndex + 1) % _spectatablePlayers.Count;
            SetTarget(_spectatablePlayers[_currentTargetIndex].transform);
        }

        [Client]
        private void SetTarget(Transform target)
        {
            _currentTarget = target;
            transform.position = target.position + Vector3.up * 5f;
            transform.LookAt(target);
        }

        [ObserversRpc]
        public void AddPlayerToSpectatableList(NetworkObject playerObject)
        {
            if (!_spectatablePlayers.Contains(playerObject))
            {
                _spectatablePlayers.Add(playerObject);
            }
        }

        [ObserversRpc]
        public void RemovePlayerFromSpectatableList(NetworkObject playerObject)
        {
            if (_spectatablePlayers.Contains(playerObject))
            {
                _spectatablePlayers.Remove(playerObject);
                if (_currentTarget == playerObject.transform)
                {
                    SwitchToNextTarget();
                }
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (base.IsOwner)
            {
                _moveAction?.Disable();
                _lookAction?.Disable();
                _fastMoveAction?.Disable();
                _zoomAction?.Disable();
                _switchTargetAction?.Disable();

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void OnDestroy()
        {
            if (base.IsOwner)
            {
                _moveAction?.Dispose();
                _lookAction?.Dispose();
                _fastMoveAction?.Dispose();
                _zoomAction?.Dispose();
                _switchTargetAction?.Dispose();
            }
        }
    }
}