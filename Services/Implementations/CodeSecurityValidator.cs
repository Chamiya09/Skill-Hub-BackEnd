using System.Text.RegularExpressions;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    public sealed class SecurityValidationResult
    {
        public bool IsAllowed { get; set; } = true;
        public string? ViolationReason { get; set; }
        public string? MatchedPattern { get; set; }

        public static SecurityValidationResult Allowed() => new() { IsAllowed = true };

        public static SecurityValidationResult Denied(string reason, string pattern) => new()
        {
            IsAllowed = false,
            ViolationReason = reason,
            MatchedPattern = pattern
        };
    }

    /// <summary>
    /// Validates submitted candidate source code against unauthorized system calls,
    /// file system operations, process spawning, network sockets, and dynamic code evaluation.
    /// </summary>
    public static class CodeSecurityValidator
    {
        private static readonly RegexOptions RegexOpts = RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled;

        // Python Patterns
        private static readonly (Regex Pattern, string Reason)[] PythonRules = new[]
        {
            (new Regex(@"\b(import|from)\s+(os|subprocess|shutil|socket|http|urllib|requests|pty|ctypes|winreg|multiprocessing|threading|signal)\b", RegexOpts),
                "Importing system, network, or process modules is prohibited."),
            (new Regex(@"\b__import__\s*\(", RegexOpts),
                "Dynamic module importing via '__import__' is prohibited."),
            (new Regex(@"\b(eval|exec|compile|breakpoint)\s*\(", RegexOpts),
                "Dynamic code evaluation (eval/exec/compile) is prohibited."),
            (new Regex(@"\bopen\s*\(", RegexOpts),
                "File system access via 'open()' is prohibited in the assessment environment."),
            (new Regex(@"\b(os\.(system|popen|spawn|kill|remove|unlink|rmdir|mkdir|rename|chmod|chown|walk|environ))\b", RegexOpts),
                "OS-level operations are prohibited."),
            (new Regex(@"\bsys\.(exit|_getframe|modules)\b", RegexOpts),
                "Process termination or internal inspection via 'sys' is prohibited.")
        };

        // JavaScript / TypeScript Patterns
        private static readonly (Regex Pattern, string Reason)[] JavaScriptRules = new[]
        {
            (new Regex(@"\brequire\s*\(\s*['""](fs|child_process|net|http|https|os|cluster|worker_threads|dns|vm)['""]\s*\)", RegexOpts),
                "Accessing Node.js system, network, or filesystem modules is prohibited."),
            (new Regex(@"\bimport\s+.*\s+from\s+['""](fs|child_process|net|http|https|os|cluster|worker_threads|dns|vm)['""]", RegexOpts),
                "Importing system, network, or filesystem modules is prohibited."),
            (new Regex(@"\b(eval|Function)\s*\(", RegexOpts),
                "Dynamic code evaluation (eval/Function) is prohibited."),
            (new Regex(@"\bprocess\.(exit|kill|env|binding|dlopen)\b", RegexOpts),
                "Process control and environment inspection via 'process' is prohibited.")
        };

        // C# Patterns
        private static readonly (Regex Pattern, string Reason)[] CSharpRules = new[]
        {
            (new Regex(@"\busing\s+(System\.IO|System\.Diagnostics|System\.Net|System\.Reflection|Microsoft\.Win32)\b", RegexOpts),
                "Using filesystem, process, network, or reflection namespaces is prohibited."),
            (new Regex(@"\bSystem\.IO\.(File|Directory|Path|StreamReader|StreamWriter|FileStream)\b", RegexOpts),
                "Direct file system operations via 'System.IO' are prohibited."),
            (new Regex(@"\bSystem\.Diagnostics\.Process\b", RegexOpts),
                "Process execution via 'System.Diagnostics.Process' is prohibited."),
            (new Regex(@"\bSystem\.Net\.(WebClient|HttpWebRequest|Sockets)\b", RegexOpts),
                "Network operations via 'System.Net' are prohibited."),
            (new Regex(@"\bDllImport\b", RegexOpts),
                "Native library interop via 'DllImport' is prohibited.")
        };

        // Java Patterns
        private static readonly (Regex Pattern, string Reason)[] JavaRules = new[]
        {
            (new Regex(@"\bimport\s+(java\.io\.File|java\.nio\.file|java\.lang\.ProcessBuilder|java\.lang\.Runtime|java\.net\.|javax\.net\.)", RegexOpts),
                "Importing filesystem, process, or network packages is prohibited."),
            (new Regex(@"\b(ProcessBuilder|Runtime\.getRuntime\(\))\b", RegexOpts),
                "Process execution via ProcessBuilder/Runtime is prohibited."),
            (new Regex(@"\bSystem\.exit\s*\(", RegexOpts),
                "Process termination via 'System.exit()' is prohibited."),
            (new Regex(@"\bjava\.io\.(File|FileInputStream|FileOutputStream|FileReader|FileWriter|RandomAccessFile)\b", RegexOpts),
                "Direct file system operations are prohibited.")
        };

        // C++ Patterns
        private static readonly (Regex Pattern, string Reason)[] CppRules = new[]
        {
            (new Regex(@"#\s*include\s*<(\s*fstream|windows\.h|unistd\.h|sys\/|winsock2\.h|direct\.h\s*)>", RegexOpts),
                "Including filesystem, network, or OS header files is prohibited."),
            (new Regex(@"\b(system|popen|_popen|exec|fork|kill)\s*\(", RegexOpts),
                "Executing system commands or process spawning is prohibited.")
        };

        // Go Patterns
        private static readonly (Regex Pattern, string Reason)[] GoRules = new[]
        {
            (new Regex(@"""(os|os/exec|net|net/http|io/ioutil|syscall)""", RegexOpts),
                "Importing system, network, or process packages is prohibited.")
        };

        public static SecurityValidationResult Validate(string? code, string? language)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return SecurityValidationResult.Allowed();
            }

            var normalizedLang = (language ?? string.Empty).Trim().ToLowerInvariant();

            (Regex Pattern, string Reason)[] rules = normalizedLang switch
            {
                "python" or "py" or "python3" => PythonRules,
                "javascript" or "js" or "node" or "typescript" or "ts" => JavaScriptRules,
                "csharp" or "cs" or "c#" or "csharp.net" or "dotnet" => CSharpRules,
                "java" => JavaRules,
                "cpp" or "c++" => CppRules,
                "go" or "golang" => GoRules,
                _ => PythonRules
            };

            foreach (var (regex, reason) in rules)
            {
                var match = regex.Match(code);
                if (match.Success)
                {
                    return SecurityValidationResult.Denied(
                        $"Security Violation: {reason} Detected: '{match.Value.Trim()}'.",
                        match.Value.Trim()
                    );
                }
            }

            return SecurityValidationResult.Allowed();
        }
    }
}

