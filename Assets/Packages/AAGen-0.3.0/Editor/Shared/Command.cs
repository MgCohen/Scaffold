using System;

namespace AAGen
{
    /// <summary>
    /// Represents a command to process.
    /// </summary>
    public struct Command
    {
        #region Fields
        /// <summary>
        /// The operation to process.
        /// </summary>
        public Action Action;

        /// <summary>
        /// Relevant information about the operation.
        /// </summary>
        public string Info;
        #endregion
    }
}
