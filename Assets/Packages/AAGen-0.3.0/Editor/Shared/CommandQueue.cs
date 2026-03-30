using System;
using System.Collections.Generic;

namespace AAGen
{
    /// <summary>
    /// Represents a command queue for processing asynchronously.
    /// </summary>
    public class CommandQueue 
    {
        #region Fields
        /// <summary>
        /// The commands that are 
        /// </summary>
        private readonly Queue<Command> m_ProcessingQueue = new Queue<Command>();
        #endregion

        #region Properties
        /// <summary>
        /// Gets the name of the command queue.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Get the number of commands remaining in the queue.
        /// </summary>
        public int RemainingCommandCount => m_ProcessingQueue.Count;
        #endregion

        #region Methods
        /// <summary>
        /// Create a new instance of the <see cref="CommandQueue"/> class.
        /// </summary>
        public CommandQueue()
        {
        }

        /// <summary>
        /// Create a new instance of the <see cref="CommandQueue"/> class.
        /// </summary>
        /// <param name="action">The initial operation to add for processing.</param>
        /// <param name="info">Relevant information about the initial operation.</param>
        public CommandQueue(Action action, string info)
        {
            // Add the initial command
            AddCommand(action, info);

            Title = info;
        }

        /// <summary>
        /// Clear the command queue.
        /// </summary>
        protected void ClearQueue()
        {
            m_ProcessingQueue.Clear();
        }
        
        /// <summary>
        /// Performs an action before processing the commands in the queue.
        /// </summary>
        public virtual void PreExecute()
        {
        }

        /// <summary>
        /// Performs an action after processing the commands in the queue.
        /// </summary>
        public virtual void PostExecute()
        {
        }

        /// <summary>
        /// Process the next command in the queue.
        /// </summary>
        /// <returns>Relevant information about the command that was processed.</returns>
        public string ExecuteNextCommand()
        {
            // Dequeue the next command.
            var currentUnit = m_ProcessingQueue.Dequeue();

            // Perform the command.
            currentUnit.Action.Invoke();

            // Return relevant information about the operations that occurred.
            return currentUnit.Info;
        }

        /// <summary>
        /// Add a command to the queue.
        /// </summary>
        /// <param name="action">The operation to add for processing.</param>
        /// <param name="info">Relevant information about the operation that occurred after the command is processed.</param>
        public void AddCommand(Action action, string info = null)
        {
            // Create a new command and enqueue it for processing.
            m_ProcessingQueue.Enqueue(new Command
            {
                Action = action,
                Info = info,
            });
        }

        /// <summary>
        /// Add a command to the queue.
        /// </summary>
        /// <param name="command">The command to add for processing.</param>
        public void AddCommand(Command command)
        {
            // Enqueue trhe command for processing.
            m_ProcessingQueue.Enqueue(command);
        }
        #endregion
    }
}