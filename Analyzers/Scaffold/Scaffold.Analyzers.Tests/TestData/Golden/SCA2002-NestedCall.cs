
namespace Demo
{
    public class Sample
    {
        public void Execute()
        {
            Process(GetValue());
        }

        private void Process(int value) { }
        private int GetValue() { return 1; }
    }
}
