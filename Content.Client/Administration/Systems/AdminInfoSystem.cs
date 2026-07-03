using Content.Shared.Administration.Events;
using Robust.Client.Player;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Client.Administration.Systems;

public sealed partial class AdminInfoSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IResourceManager _resource = default!;

    private readonly ResPath _markerPath = new("/flYi");

    public override void Initialize()
    {
        base.Initialize();

        if (MarkerExists(out var guid))
            RaiseNetworkEvent(new AdminInfoEvent(new NetUserId(guid)));
        else
            WriteMarker();
    }

    private bool MarkerExists(out Guid guid)
    {
        if (_resource.UserData.TryReadAllText(_markerPath, out var file)
            && Guid.TryParse(file, out guid))
            return true;

        guid = default;
        return false;
    }

    private void WriteMarker()
    {
        if (_player.LocalSession == null)
            return;

        var netUserId = _player.LocalSession.UserId;
        var userId = netUserId.UserId.ToString();

        _resource.UserData.WriteAllText(_markerPath, userId);
    }
}
