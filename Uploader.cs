using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ClipLift
{
    /// <summary>Runs the Windows OpenSSH client (scp / ssh) using the user's ~/.ssh/config and keys.</summary>
    internal static class Uploader
    {
        /// <summary>Uploads the files and returns their remote file names.</summary>
        public static List<string> Upload(Profile profile, IReadOnlyList<string> files, int timeoutSeconds)
        {
            var args = new List<string> { "-q" };
            args.AddRange(CommonOptions(timeoutSeconds));
            args.AddRange(PortOption("-P", profile));
            args.AddRange(files);
            args.Add(profile.Host.Trim() + ":" + profile.RemoteDir.Trim().TrimEnd('/') + "/");

            Run(Tool("scp.exe"), args, TimeSpan.FromMinutes(2));
            return files.Select(Path.GetFileName).ToList();
        }

        /// <summary>Connects to the host and creates the remote directory.</summary>
        public static void TestConnection(Profile profile, int timeoutSeconds)
        {
            var args = new List<string>(CommonOptions(timeoutSeconds));
            args.AddRange(PortOption("-p", profile));
            args.Add(profile.Host.Trim());
            args.Add("mkdir -p -- " + ShellQuote(profile.RemoteDir.Trim()));

            Run(Tool("ssh.exe"), args, TimeSpan.FromSeconds(timeoutSeconds + 20));
        }

        private static IEnumerable<string> CommonOptions(int timeoutSeconds)
        {
            return new[]
            {
                "-o", "BatchMode=yes",
                "-o", "ConnectTimeout=" + timeoutSeconds.ToString(CultureInfo.InvariantCulture),
            };
        }

        /// <summary>
        /// The default port is not passed, so ~/.ssh/config (Port, ProxyJump, ProxyCommand) stays in charge;
        /// any other port overrides it.
        /// </summary>
        private static IEnumerable<string> PortOption(string flag, Profile profile)
        {
            if (profile.Port <= 0 || profile.Port == Profile.DefaultPort)
                return Enumerable.Empty<string>();
            return new[] { flag, profile.Port.ToString(CultureInfo.InvariantCulture) };
        }

        private static string Tool(string exe)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", exe);
            return File.Exists(path) ? path : exe;
        }

        private static string ShellQuote(string s) => "'" + s.Replace("'", "'\\''") + "'";

        private static void Run(string exe, IEnumerable<string> args, TimeSpan timeout)
        {
            string name = Path.GetFileNameWithoutExtension(exe);
            var psi = new ProcessStartInfo(exe, string.Join(" ", args.Select(QuoteArg)))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            Process process;
            try
            {
                process = Process.Start(psi);
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException(
                    "Cannot start " + name + ": " + ex.Message + ". Is the Windows OpenSSH client installed?", ex);
            }

            using (process)
            {
                process.StandardInput.Close();
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                {
                    try { process.Kill(); } catch (InvalidOperationException) { } catch (Win32Exception) { }
                    throw new TimeoutException(name + " did not finish within " + (int)timeout.TotalSeconds + " seconds.");
                }
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string output = (stderr.Result + "\n" + stdout.Result).Trim();
                    throw new InvalidOperationException(
                        name + " exited with code " + process.ExitCode + (output.Length > 0 ? ":\n" + output : "."));
                }
            }
        }

        /// <summary>Quotes one argument following the CommandLineToArgvW / MSVC CRT rules.</summary>
        private static string QuoteArg(string arg)
        {
            if (arg.Length > 0 && arg.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
                return arg;

            var sb = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }
                if (c == '"')
                    sb.Append('\\', backslashes * 2 + 1);
                else
                    sb.Append('\\', backslashes);
                sb.Append(c);
                backslashes = 0;
            }
            sb.Append('\\', backslashes * 2);
            sb.Append('"');
            return sb.ToString();
        }
    }
}
