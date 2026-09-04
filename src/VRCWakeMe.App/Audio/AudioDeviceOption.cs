namespace VRCWakeMe.App.Audio;

public sealed class AudioDeviceOption
{
    public AudioDeviceOption(int number, string name)
    {
        Number = number;
        Name = name;
    }

    public int Number { get; }
    public string Name { get; }

    public override string ToString() => Name;
}
