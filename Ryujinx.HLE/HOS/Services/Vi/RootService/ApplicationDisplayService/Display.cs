namespace Ryujinx.HLE.HOS.Services.Vi
{
    class Display
    {
        public string Name { get; private set; }

        public Display(string name)
        {
            Name = name;
        }
    }
}