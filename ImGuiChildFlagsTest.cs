using System;
using ImGuiNET;

class ImGuiChildFlagsTest
{
    static void Main()
    {
        Console.WriteLine("Available ImGuiChildFlags enum values:");
        Console.WriteLine("=====================================");
        
        foreach (var flag in Enum.GetValues(typeof(ImGuiChildFlags)))
        {
            Console.WriteLine($"{flag} = {(int)flag}");
        }
    }
}