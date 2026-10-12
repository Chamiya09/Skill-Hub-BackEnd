using Skill_Hub_BackEnd.Services.Assessments;
using Xunit;

namespace Skill_Hub_BackEnd.Tests.Unit.Services
{
    public class CodeSecurityValidatorTests
    {
        [Theory]
        [InlineData("import os\nos.system('rm -rf /')", "python")]
        [InlineData("import subprocess\nsubprocess.Popen(['ls'])", "python")]
        [InlineData("eval('__import__(\"os\").system(\"whoami\")')", "python")]
        [InlineData("with open('/etc/passwd', 'r') as f:\n    print(f.read())", "python")]
        [InlineData("import sys\nsys.exit(0)", "python")]
        public void Validate_PythonMaliciousCode_ReturnsDenied(string code, string language)
        {
            var result = CodeSecurityValidator.Validate(code, language);
            Assert.False(result.IsAllowed);
            Assert.NotNull(result.ViolationReason);
            Assert.Contains("Security Violation", result.ViolationReason);
        }

        [Theory]
        [InlineData("def two_sum(nums, target):\n    seen = {}\n    for i, n in enumerate(nums):\n        if target - n in seen:\n            return [seen[target - n], i]\n        seen[n] = i\n    return []", "python")]
        [InlineData("class Solution:\n    def is_valid(self, s: str) -> bool:\n        stack = []\n        for c in s:\n            if c == '(': stack.append(')')\n            elif not stack or stack.pop() != c: return False\n        return not stack", "python")]
        public void Validate_PythonSafeCode_ReturnsAllowed(string code, string language)
        {
            var result = CodeSecurityValidator.Validate(code, language);
            Assert.True(result.IsAllowed);
            Assert.Null(result.ViolationReason);
        }

        [Theory]
        [InlineData("const fs = require('fs'); fs.readFileSync('/etc/shadow');", "javascript")]
        [InlineData("const cp = require('child_process'); cp.execSync('calc');", "javascript")]
        [InlineData("eval('console.log(process.env)')", "javascript")]
        [InlineData("process.exit(1);", "javascript")]
        public void Validate_JavaScriptMaliciousCode_ReturnsDenied(string code, string language)
        {
            var result = CodeSecurityValidator.Validate(code, language);
            Assert.False(result.IsAllowed);
            Assert.NotNull(result.ViolationReason);
        }

        [Theory]
        [InlineData("function binarySearch(arr, target) {\n  let l = 0, r = arr.length - 1;\n  while (l <= r) {\n    const mid = (l + r) >> 1;\n    if (arr[mid] === target) return mid;\n    if (arr[mid] < target) l = mid + 1; else r = mid - 1;\n  }\n  return -1;\n}", "javascript")]
        public void Validate_JavaScriptSafeCode_ReturnsAllowed(string code, string language)
        {
            var result = CodeSecurityValidator.Validate(code, language);
            Assert.True(result.IsAllowed);
            Assert.Null(result.ViolationReason);
        }

        [Theory]
        [InlineData("using System.IO;\nclass Test { void M() { File.ReadAllText(\"c:/secret.txt\"); } }", "csharp")]
        [InlineData("using System.Diagnostics;\nclass Test { void M() { Process.Start(\"cmd.exe\"); } }", "csharp")]
        [InlineData("class Test { void M() { System.IO.File.Delete(\"app.db\"); } }", "csharp")]
        public void Validate_CSharpMaliciousCode_ReturnsDenied(string code, string language)
        {
            var result = CodeSecurityValidator.Validate(code, language);
            Assert.False(result.IsAllowed);
            Assert.NotNull(result.ViolationReason);
        }

        [Fact]
        public void Validate_CSharpSafeCode_ReturnsAllowed()
        {
            var safeCode = @"
using System;
using System.Collections.Generic;
using System.Linq;

public class Solution {
    public int MaxSubArray(int[] nums) {
        int maxSoFar = nums[0], currMax = nums[0];
        for (int i = 1; i < nums.Length; i++) {
            currMax = Math.Max(nums[i], currMax + nums[i]);
            maxSoFar = Math.Max(maxSoFar, currMax);
        }
        return maxSoFar;
    }
}";
            var result = CodeSecurityValidator.Validate(safeCode, "csharp");
            Assert.True(result.IsAllowed);
            Assert.Null(result.ViolationReason);
        }

        [Theory]
        [InlineData("import java.lang.ProcessBuilder;\nclass Solution {}", "java")]
        [InlineData("class Solution { void M() { Runtime.getRuntime().exec(\"ls\"); } }", "java")]
        [InlineData("class Solution { void M() { System.exit(0); } }", "java")]
        public void Validate_JavaMaliciousCode_ReturnsDenied(string code, string language)
        {
            var result = CodeSecurityValidator.Validate(code, language);
            Assert.False(result.IsAllowed);
        }

        [Theory]
        [InlineData("#include <windows.h>\nint main() { return 0; }", "cpp")]
        [InlineData("int main() { system(\"ls\"); return 0; }", "cpp")]
        public void Validate_CppMaliciousCode_ReturnsDenied(string code, string language)
        {
            var result = CodeSecurityValidator.Validate(code, language);
            Assert.False(result.IsAllowed);
        }

        [Theory]
        [InlineData("package main\nimport \"os/exec\"\nfunc main() {}", "go")]
        [InlineData("package main\nimport \"syscall\"\nfunc main() {}", "go")]
        public void Validate_GoMaliciousCode_ReturnsDenied(string code, string language)
        {
            var result = CodeSecurityValidator.Validate(code, language);
            Assert.False(result.IsAllowed);
        }

        [Fact]
        public void Validate_NullOrEmptyCode_ReturnsAllowed()
        {
            var result1 = CodeSecurityValidator.Validate(null, "python");
            var result2 = CodeSecurityValidator.Validate("", "csharp");
            var result3 = CodeSecurityValidator.Validate("   ", "javascript");

            Assert.True(result1.IsAllowed);
            Assert.True(result2.IsAllowed);
            Assert.True(result3.IsAllowed);
        }
    }
}
