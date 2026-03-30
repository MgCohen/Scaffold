using NUnit.Framework;
using System;
using AAGen;

namespace AAGen.Tests
{
    public class CommandQueueTests
    {
        CommandQueue m_CommandQueue;
        int m_Counter;

        [SetUp]
        public void Setup()
        {
            m_CommandQueue = new CommandQueue();
            m_Counter = 0;
        }

        [Test]
        public void AddCommand_IncreasesRemainingCount()
        {
            m_CommandQueue.AddCommand(() => m_Counter++, "Increment");
            Assert.AreEqual(1, m_CommandQueue.RemainingCommandCount);
        }

        [Test]
        public void ExecuteNextCommand_InvokesActionAndReturnsInfo()
        {
            m_CommandQueue.AddCommand(() => m_Counter += 5, "Add 5");
            string result = m_CommandQueue.ExecuteNextCommand();

            Assert.AreEqual("Add 5", result);
            Assert.AreEqual(5, m_Counter);
        }

        [Test]
        public void ExecuteMultipleCommands_ExecutesInOrder()
        {
            m_CommandQueue.AddCommand(() => m_Counter += 1, "Add 1");
            m_CommandQueue.AddCommand(() => m_Counter += 2, "Add 2");
            m_CommandQueue.AddCommand(() => m_Counter += 3, "Add 3");

            Assert.AreEqual("Add 1", m_CommandQueue.ExecuteNextCommand());
            Assert.AreEqual(1, m_Counter);

            Assert.AreEqual("Add 2", m_CommandQueue.ExecuteNextCommand());
            Assert.AreEqual(3, m_Counter);

            Assert.AreEqual("Add 3", m_CommandQueue.ExecuteNextCommand());
            Assert.AreEqual(6, m_Counter);
        }

        [Test]
        public void Constructor_WithAction_AddsSingleCommand()
        {
            var q = new CommandQueue(() => m_Counter = 10, "Set to 10");
            Assert.AreEqual("Set to 10", q.Title);
            Assert.AreEqual(1, q.RemainingCommandCount);

            string result = q.ExecuteNextCommand();
            Assert.AreEqual("Set to 10", result);
            Assert.AreEqual(10, m_Counter);
        }

        [Test]
        public void AddCommandStruct_QueuesAndExecutes()
        {
            var command = new Command
            {
                Action = () => m_Counter += 7,
                Info = "Add 7"
            };

            m_CommandQueue.AddCommand(command);
            Assert.AreEqual("Add 7", m_CommandQueue.ExecuteNextCommand());
            Assert.AreEqual(7, m_Counter);
        }
    }
}