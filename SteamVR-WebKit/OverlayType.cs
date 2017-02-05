namespace SteamVR_WebKit
{
    public enum OverlayType
    {
        /// <summary>
        /// Only show the overlay in-game, not in the dashboard
        /// </summary>
        InGame,

        /// <summary>
        /// Only show the overlay in the dashboard
        /// </summary>
        Dashboard,

        /// <summary>
        /// Show the overlay both as a dashboard overlay and in-game (useful for things that require interaction but you want shown in-game)
        /// </summary>
        Both
    }
}