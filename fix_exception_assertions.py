#!/usr/bin/env python3
import re
import os

def fix_exception_assertions(content):
    """Fix exception assertion patterns to use Action/Func pattern"""
    lines = content.split('\n')
    fixed_lines = []
    i = 0
    
    while i < len(lines):
        line = lines[i]
        
        # Pattern 1: (() => someCode).Should().Throw<Exception>()
        # This pattern appears on a single line
        match = re.match(r'^(\s*)\(\(\) => (.*?)\)\.Should\(\)\.Throw<([^>]+)>\(\)(.*?)$', line)
        if match:
            indent, code, exception_type, rest = match.groups()
            # Check if the code part already has .Should() in it (malformed)
            if '.Should()' in code:
                # Extract the actual code part before .Should()
                code_parts = code.split('.Should()')
                if len(code_parts) > 1:
                    code = code_parts[0]
            fixed_lines.append(f"{indent}// Act & Assert")
            fixed_lines.append(f"{indent}Action act = () => {code};")
            fixed_lines.append(f"{indent}act.Should().Throw<{exception_type}>(){rest}")
            i += 1
            continue
            
        # Pattern 2: Standalone line that's just a lambda expression without proper statement
        # e.g., (() => mapper.MapEntityToParameters(null).Should().Throw<ArgumentNullException>());
        match = re.match(r'^(\s*)\(\(\) => (.*?)\.Should\(\)\.Throw<([^>]+)>\(\)\);?$', line)
        if match:
            indent, code, exception_type = match.groups()
            fixed_lines.append(f"{indent}// Act & Assert")
            fixed_lines.append(f"{indent}Action act = () => {code};")
            fixed_lines.append(f"{indent}act.Should().Throw<{exception_type}>();")
            i += 1
            continue
            
        # Pattern 3: Multi-line lambda with opening on current line
        if '(() => {' in line or '(() =>' in line:
            indent_match = re.match(r'^(\s*)', line)
            indent = indent_match.group(1) if indent_match else ''
            
            # Check if this is part of an incorrect throw assertion
            j = i
            lambda_lines = []
            while j < len(lines):
                lambda_lines.append(lines[j])
                if '}).Should().Throw' in lines[j] or '});' in lines[j]:
                    break
                j += 1
            
            if j < len(lines) and '.Should().Throw' in ''.join(lambda_lines):
                # This is an exception assertion that needs fixing
                # Extract the lambda body
                lambda_body = []
                for k in range(i + 1, j):
                    lambda_body.append(lines[k])
                
                # Extract exception type from the throw assertion
                throw_match = re.search(r'\.Should\(\)\.Throw<([^>]+)>', lines[j])
                if throw_match:
                    exception_type = throw_match.group(1)
                    fixed_lines.append(f"{indent}// Act & Assert")
                    fixed_lines.append(f"{indent}Action act = () =>")
                    fixed_lines.append(f"{indent}{{")
                    for body_line in lambda_body:
                        fixed_lines.append(body_line)
                    fixed_lines.append(f"{indent}}};")
                    fixed_lines.append(f"{indent}act.Should().Throw<{exception_type}>();")
                    i = j + 1
                    continue
                    
        # Pattern 4: Fix incorrect usage like: var config = SqliteConfiguration.FromJsonFileRequired(nonExistentConfig).Should().Throw<FileNotFoundException>();
        match = re.match(r'^(\s*)var\s+\w+\s*=\s*(.*?)\.Should\(\)\.Throw<([^>]+)>\(\);?$', line)
        if match:
            indent, code, exception_type = match.groups()
            fixed_lines.append(f"{indent}// Act & Assert")
            fixed_lines.append(f"{indent}Action act = () => {code};")
            fixed_lines.append(f"{indent}act.Should().Throw<{exception_type}>();")
            i += 1
            continue
            
        fixed_lines.append(line)
        i += 1
    
    return '\n'.join(fixed_lines)

# Process specific files with exception assertions
test_dir = '/mnt/e/work/github/crp/persistence-lib/UnitTest'

for root, dirs, files in os.walk(test_dir):
    for file in files:
        if file.endswith('.cs'):
            filepath = os.path.join(root, file)
            with open(filepath, 'r', encoding='utf-8') as f:
                content = f.read()
            
            original_content = content
            content = fix_exception_assertions(content)
            
            if content != original_content:
                with open(filepath, 'w', encoding='utf-8') as f:
                    f.write(content)
                print(f"Fixed {filepath}")