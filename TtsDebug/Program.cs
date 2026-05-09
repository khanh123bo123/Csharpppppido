using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        string edgeTtsExe = "/Users/dangkhoa/Library/Python/3.9/bin/edge-tts";
        string text = "Đây là một thử nghiệm thuyết minh tiếng Việt.";
        string voice = "vi-VN-HoaiMyNeural";
        string outputPath = "test_output.mp3";
        string textFilePath = "test_text.txt";

        File.WriteAllText(textFilePath, text);

        var startInfo = new ProcessStartInfo
        {
            FileName = edgeTtsExe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("--file");
        startInfo.ArgumentList.Add(textFilePath);
        startInfo.ArgumentList.Add("--voice");
        startInfo.ArgumentList.Add(voice);
        startInfo.ArgumentList.Add("--write-media");
        startInfo.ArgumentList.Add(outputPath);

        Console.WriteLine($"Running: {edgeTtsExe} --file {textFilePath} --voice {voice} --write-media {outputPath}");

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            Console.WriteLine("Failed to start process.");
            return;
        }

        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Console.WriteLine($"Exit Code: {process.ExitCode}");
        Console.WriteLine($"Stdout: {stdout}");
        Console.WriteLine($"Stderr: {stderr}");

        if (File.Exists(outputPath))
        {
            Console.WriteLine("Success: Output file created.");
        }
        else
        {
            Console.WriteLine("Failure: Output file not created.");
        }
    }
}
