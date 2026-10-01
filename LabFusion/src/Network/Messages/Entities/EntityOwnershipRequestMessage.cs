namespace LabFusion.Network;

public class EntityOwnershipRequestMessage : NativeMessageHandler
{
    public override byte Tag => NativeMessageTag.EntityOwnershipRequest;

    public override ExpectedReceiverType ExpectedReceiver => ExpectedReceiverType.ServerOnly;

    protected override void OnHandleMessage(ReceivedMessage received)
    {
        // Read request
        var data = received.ReadData<EntityPlayerData>();

        if (!LabFusion.Safety.AntiCheat.FusionAntiCheat.AllowOwnershipRequest(received, data))
        {
            return;
        }

        // Send response
        var response = new EntityPlayerData()
        {
            PlayerID = data.PlayerID,
            Entity = new(data.Entity.ID),
        };

        MessageRelay.RelayNative(response, NativeMessageTag.EntityOwnershipResponse, CommonMessageRoutes.ReliableToClients);
    }
}
