namespace Scaffold.Records.Samples
{
    public class RecordsUseCases
    {
        public string UseCaseInitPropertyWithObjectInitializer()
        {
            PersonRecord person = new PersonRecord { Name = "Alice", Age = 30 };
            return person.Name;
        }

        public bool UseCaseInitPropertyIsReadAfterConstruction()
        {
            PersonRecord person = new PersonRecord { Name = "Bob", Age = 25 };
            bool nameIsSet = person.Name == "Bob";
            bool ageIsSet = person.Age == 25;
            return nameIsSet && ageIsSet;
        }

        private struct PersonRecord
        {
            public string Name { get; init; }
            public int Age { get; init; }
        }
    }
}
