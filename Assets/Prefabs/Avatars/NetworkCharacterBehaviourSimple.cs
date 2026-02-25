using System.Linq;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using Meta.XR.Movement.Networking;

public class NetworkCharacterBehaviourSimple : NetworkBehaviour, INetworkCharacterBehaviour
{
    public GameObject CharacterPrefab => null;
    public ulong MetaId => 0;
    public int CharacterId => 1;
    public ulong LocalClientId => NetworkManager.Singleton.LocalClientId;
    public ulong[] ClientIds => NetworkManager.Singleton.ConnectedClientsIds.ToArray();
    public float NetworkTime => (float)NetworkManager.Singleton.ServerTime.Time;
    public float RenderTime => (float)NetworkManager.Singleton.ServerTime.Time;
    public float DeltaTime => Time.deltaTime;
    public bool HasInputAuthority => IsOwner;
    public bool FollowCameraRig { get; set; }

    private int _dataSentCount = 0;
    private int _dataReceivedCount = 0;
    private int _ackSentCount = 0;
    private int _ackReceivedCount = 0;

    public void ReceiveStreamData(ulong clientId, bool isReliable, NativeArray<byte> bytes)
    {
        if (IsServer)
        {
            _dataSentCount++;
            Debug.Log($"[Behaviour] HOST sending data #{_dataSentCount} ({bytes.Length} bytes) to client {clientId}");
            SendDataToClientsServerRpc(clientId, bytes.ToArray());
        }
    }

    public void ReceiveStreamAck(ulong clientId, int ack)
    {
        if (IsServer)
        {
            _ackSentCount++;
            Debug.Log($"[Behaviour] HOST sending ACK #{_ackSentCount} (ack={ack}) from client {clientId}");
            SendAckToClientsServerRpc(clientId, ack);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendDataToClientsServerRpc(ulong senderClientId, byte[] data, ServerRpcParams rpcParams = default)
    {
        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { }
            }
        };

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (clientId != senderClientId)
            {
                clientRpcParams.Send.TargetClientIds = new ulong[] { clientId };
                ReceiveDataClientRpc(data, clientRpcParams);
            }
        }
    }

    [ClientRpc]
    private void ReceiveDataClientRpc(byte[] data, ClientRpcParams rpcParams = default)
    {
        _dataReceivedCount++;
        Debug.Log($"[Behaviour] CLIENT received data #{_dataReceivedCount} ({data.Length} bytes)");
        
        var handler = GetComponent<NetworkCharacterHandler>();
        if (handler != null)
        {
            var nativeData = new NativeArray<byte>(data, Allocator.Temp);
            handler.ReceiveData(nativeData);
            nativeData.Dispose();
            Debug.Log($"[Behaviour] ✓ Data passed to NetworkCharacterHandler");
        }
        else
        {
            Debug.LogError($"[Behaviour] ✗ NetworkCharacterHandler NOT FOUND!");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SendAckToClientsServerRpc(ulong senderClientId, int ack, ServerRpcParams rpcParams = default)
    {
        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { }
            }
        };

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (clientId != senderClientId)
            {
                clientRpcParams.Send.TargetClientIds = new ulong[] { clientId };
                ReceiveAckClientRpc(senderClientId, ack, clientRpcParams);
            }
        }
    }

    [ClientRpc]
    private void ReceiveAckClientRpc(ulong senderClientId, int ack, ClientRpcParams rpcParams = default)
    {
        _ackReceivedCount++;
        Debug.Log($"[Behaviour] CLIENT received ACK #{_ackReceivedCount} (ack={ack})");
        
        var handler = GetComponent<NetworkCharacterHandler>();
        if (handler != null)
        {
            handler.ReceiveAck(senderClientId, ack);
            Debug.Log($"[Behaviour] ✓ ACK passed to NetworkCharacterHandler");
        }
        else
        {
            Debug.LogError($"[Behaviour] ✗ NetworkCharacterHandler NOT FOUND!");
        }
    }
}
