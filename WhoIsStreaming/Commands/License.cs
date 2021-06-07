using System.IO;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;

[Command(nameof(Licenses), Description = "Print licenses of software used to make this program")]
public class Licenses : ICommand
{
    public ValueTask ExecuteAsync (IConsole console) 
    {
        try 
        {
            var fileContents = File.ReadAllLines("Licenses.txt");
            foreach (var line in fileContents)
            {
                console.Output.WriteLine(line);
            }
            return default;
        }
        catch (FileNotFoundException ex)
        {
            console.Output.WriteLine($"It was not possible to find the license file {ex.FileName}");
            return default;
        }
    }
}