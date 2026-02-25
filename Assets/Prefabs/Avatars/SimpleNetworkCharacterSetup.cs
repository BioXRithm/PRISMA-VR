using UnityEngine;
using Unity.Netcode;
using Meta.XR.Movement.Networking;
using Meta.XR.Movement.Retargeting;

[RequireComponent(typeof(NetworkCharacterHandler))]
public class SimpleNetworkCharacterSetup : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool hideHeadForOwner = true;
    [SerializeField] private bool followCameraRig = true;
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private bool syncRotation = true;
    
    private NetworkCharacterHandler _handler;
    private NetworkCharacterBehaviourSimple _behaviour;
    private NetworkCharacterRetargeter _retargeter;
    private OVRBody _ovrBody;
    private Transform _cameraRig;
    private bool _shouldFollowCameraRig;
    private MetaSourceDataProvider _metaSourceDataProvider;

    void Awake()
    {
        Debug.Log($"[Setup] Awake - GameObject: {gameObject.name}");
        
        _handler = GetComponent<NetworkCharacterHandler>();
        //_behaviour = GetComponent<NetworkCharacterBehaviourSimple>();
        _retargeter = GetComponent<NetworkCharacterRetargeter>();
        //_ovrBody = GetComponent<OVRBody>();
        _metaSourceDataProvider = GetComponent<MetaSourceDataProvider>();
        
        /**if (_behaviour == null)
        {
            _behaviour = gameObject.AddComponent<NetworkCharacterBehaviourSimple>();
        }**/
        
        Debug.Log($"[Setup] Components Found:");
        Debug.Log($"  - Handler: {_handler != null}");
        //Debug.Log($"  - Behaviour: {_behaviour != null}");
        Debug.Log($"  - Retargeter: {_retargeter != null}");
        Debug.Log($"  - OVRBody: {_ovrBody != null}");
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Debug.Log($"========================================");
        Debug.Log($"[Setup] OnNetworkSpawn START");
        Debug.Log($"  GameObject: {gameObject.name}");
        Debug.Log($"  IsOwner: {IsOwner}");
        Debug.Log($"  ClientId: {OwnerClientId}");
        Debug.Log($"========================================");

        if (IsOwner)
        {
            SetupLocalPlayer();
        }
        else
        {
            SetupRemotePlayer();
        }
        
        Debug.Log($"[Setup] ⚠️ CALLING NetworkCharacterHandler.Setup(false)...");
        _handler.Setup(instantiateCharacter: false);
        Debug.Log($"[Setup] ✓ NetworkCharacterHandler.Setup() COMPLETE");

        Debug.Log($"[Setup] OnNetworkSpawn COMPLETE ✓");
    }

    void SetupLocalPlayer()
    {
        Debug.Log("[Setup] === CONFIGURING LOCAL PLAYER ===");
        
        /**if (_ovrBody != null)
        {
            _ovrBody.enabled = true;
            Debug.Log($"[Setup] ✓ OVRBody ENABLED for local player");
        }
        else
        {
            Debug.LogError("[Setup] ✗ OVRBody NOT FOUND! Body tracking will NOT work!");
        }**/
        
        if (_retargeter != null)
        {
            _retargeter.Owner = NetworkCharacterRetargeter.Ownership.Host;
            _retargeter.enabled = true;
            Debug.Log($"[Setup] ✓ Retargeter set to HOST and ENABLED");
        }

        /**if (_behaviour != null)
        {
            _behaviour.FollowCameraRig = followCameraRig;
            _behaviour.enabled = true;
            Debug.Log($"[Setup] ✓ Behaviour ENABLED");
        }**/
        
        if (followCameraRig)
        {
            FindCameraRig();
            _shouldFollowCameraRig = (_cameraRig != null);
            
            if (_shouldFollowCameraRig)
            {
                transform.position = _cameraRig.position + positionOffset;
                if (syncRotation)
                {
                    transform.rotation = Quaternion.Euler(0, _cameraRig.eulerAngles.y, 0);
                }
            }
        }
        
        if (hideHeadForOwner)
        {
            Invoke(nameof(HideHead), 1.5f);
        }
        if (_metaSourceDataProvider != null)
        {
            _metaSourceDataProvider.enabled = true;
            Debug.Log($"[Setup] ✓ MetaSourceDataProvider ENABLED for local player");
        }
        else
        {
            Debug.LogWarning("[Setup] ⚠ MetaSourceDataProvider NOT FOUND!");
        }
    }

    void SetupRemotePlayer()
    {
        Debug.Log("[Setup] === CONFIGURING REMOTE PLAYER ===");
        
        _shouldFollowCameraRig = false;
        
        /**if (_ovrBody != null)
        {
            _ovrBody.enabled = false;
            Debug.Log($"[Setup] ✓ OVRBody DISABLED for remote player");
        }**/
        
        if (_retargeter != null)
        {
            _retargeter.Owner = NetworkCharacterRetargeter.Ownership.Client;
            _retargeter.enabled = true;
            Debug.Log($"[Setup] ✓ Retargeter set to CLIENT and ENABLED");
        }

        /**if (_behaviour != null)
        {
            _behaviour.FollowCameraRig = false;
            _behaviour.enabled = true;
            Debug.Log($"[Setup] ✓ Behaviour ENABLED for remote");
        }**/

         if (_metaSourceDataProvider != null)
        {
            _metaSourceDataProvider.enabled = false;
            Debug.Log($"[Setup] ✓ MetaSourceDataProvider DISABLED for remote player");
        }
    }

    void LateUpdate()
    {
        if (_shouldFollowCameraRig && _cameraRig != null && IsOwner)
        {
            transform.position = _cameraRig.position + positionOffset;
            
            if (syncRotation)
            {
                transform.rotation = Quaternion.Euler(0, _cameraRig.eulerAngles.y, 0);
            }
        }
    }

    void FindCameraRig()
    {
        if (OVRManager.instance != null)
        {
            var ovrCameraRig = OVRManager.instance.GetComponentInChildren<OVRCameraRig>();
            if (ovrCameraRig != null)
            {
                _cameraRig = ovrCameraRig.transform;
                Debug.Log($"[Setup] ✓ OVRCameraRig found");
                return;
            }
        }
        
        string[] possibleNames = new string[]
        {
            "[BuildingBlock] Camera Rig 1",
            "OVRCameraRig",
            "CameraRig",
            "Camera Rig"
        };
        
        foreach (string name in possibleNames)
        {
            GameObject cameraRigObj = GameObject.Find(name);
            if (cameraRigObj != null)
            {
                _cameraRig = cameraRigObj.transform;
                Debug.Log($"[Setup] ✓ Camera Rig found: {name}");
                return;
            }
        }
        
        Debug.LogWarning("[Setup] ✗ Camera Rig NOT found!");
    }

    void HideHead()
    {
        GameObject characterObj = _handler != null && _handler.Character != null ? _handler.Character : gameObject;
        Transform skeleton = characterObj.transform.Find("Skeleton");
        
        if (skeleton != null)
        {
            Transform headBone = FindHeadBone(skeleton);
            if (headBone != null)
            {
                headBone.localScale = Vector3.zero;
                Debug.Log($"[Setup] ✓ Head hidden");
            }
        }
    }

    Transform FindHeadBone(Transform parent)
    {
        if (parent.name.ToLower().Contains("head") && !parent.name.ToLower().Contains("end"))
        {
            return parent;
        }

        foreach (Transform child in parent)
        {
            Transform result = FindHeadBone(child);
            if (result != null) return result;
        }

        return null;
    }
}
