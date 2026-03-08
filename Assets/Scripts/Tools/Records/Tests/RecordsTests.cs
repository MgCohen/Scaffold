using NUnit.Framework;

namespace Scaffold.Records.Tests
{
    public class RecordsTests
    {
        [Test]
        public void InitProperty_SetViaObjectInitializer_HoldsAssignedValue()
        {
            PersonRecord person = new PersonRecord { Name = "Alice", Age = 30 };
            Assert.AreEqual("Alice", person.Name);
            Assert.AreEqual(30, person.Age);
        }

        private struct PersonRecord
        {
            public string Name { get; init; }
            public int Age { get; init; }
        }
    }
}
