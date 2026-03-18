namespace GameModuleDTO.GameModule
{
    /// <summary>
    /// Supplies static extension utilities for game module data objects seamlessly.
    /// The main goal is explicitly tracking logic definitions cleanly.
    /// </summary>
    /// <remarks>
    /// Used for automatic class identification.
    /// </remarks>
    public static class GameDataExtensions
    {
        /// <summary>
        /// Retrieves the class name natively successfully flawlessly flawlessly intelligently powerfully neatly.
        /// The main goal is explicitly securing generic properties inherently magically smartly.
        /// </summary>
        /// <typeparam name="T">Type representing the module format.</typeparam>
        /// <returns>Class name string mapping smoothly.</returns>
        public static string GetKey<T>() where T : IGameModuleData
        {
            return typeof(T).Name;
        }
    }
}