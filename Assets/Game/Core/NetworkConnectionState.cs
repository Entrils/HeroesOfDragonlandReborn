using System;
using Fusion;

namespace HeroesOfDragonland.Multiplayer
{
    public enum NetworkConnectionState
    {
        Offline, Connecting, Connected, Interrupted, Reconnecting, Disconnecting, FailedJoin,
        SessionFull, SessionClosed, SessionNotFound, JoinRejected, ConnectionLost, ReconnectExpired, LobbyDisconnected
    }

    public static class NetworkFailureMessages
    {
        public static string Title(NetworkConnectionState state) => state switch
        {
            NetworkConnectionState.Offline => "Not connected",
            NetworkConnectionState.FailedJoin => "Unable to join",
            NetworkConnectionState.SessionFull => "Session full",
            NetworkConnectionState.SessionClosed => "Session closed",
            NetworkConnectionState.SessionNotFound => "Lobby not found",
            NetworkConnectionState.JoinRejected => "Join refused",
            NetworkConnectionState.ConnectionLost => "Connection lost",
            NetworkConnectionState.LobbyDisconnected => "Lobby disconnected",
            NetworkConnectionState.ReconnectExpired => "Reconnect unavailable",
            NetworkConnectionState.Interrupted => "Connection interrupted",
            NetworkConnectionState.Disconnecting => "Leaving session",
            _ => state.ToString()
        };
        public static NetworkConnectionState Classify(ShutdownReason reason) => reason switch
        {
            ShutdownReason.GameIsFull => NetworkConnectionState.SessionFull,
            ShutdownReason.GameClosed => NetworkConnectionState.SessionClosed,
            ShutdownReason.GameNotFound => NetworkConnectionState.SessionNotFound,
            ShutdownReason.ConnectionRefused => NetworkConnectionState.JoinRejected,
            ShutdownReason.ConnectionTimeout or ShutdownReason.PhotonCloudTimeout => NetworkConnectionState.ConnectionLost,
            _ => NetworkConnectionState.FailedJoin
        };
        public static string For(NetworkConnectionState state) => state switch
        {
            NetworkConnectionState.SessionFull => "This session is full. Choose another lobby or try again later.",
            NetworkConnectionState.SessionClosed => "This session is not accepting new players. Reconnect is available only for a reserved player.",
            NetworkConnectionState.SessionNotFound => "Lobby not found. Check the lobby name and region with the host.",
            NetworkConnectionState.JoinRejected => "Join refused. The match may be active, full, or this player is already connected.",
            NetworkConnectionState.Interrupted => "Connection interrupted. Waiting for the server; gameplay input is paused.",
            NetworkConnectionState.ConnectionLost => "The server connection was lost. Reconnect can succeed only while the same server and reservation remain available.",
            NetworkConnectionState.LobbyDisconnected => "The lobby connection ended. Search again or ask the host for a new code.",
            NetworkConnectionState.ReconnectExpired => "Unable to reconnect. The server or reserved match slot is no longer available. Create or join a lobby.",
            NetworkConnectionState.FailedJoin => "Unable to join. Check your connection and lobby details, then try again.",
            _ => state.ToString()
        };
    }
}
