#!/usr/bin/env python3
import re
import os
import sys

def fix_assertion_issues(content):
    """Fix remaining FluentAssertion syntax issues"""
    lines = content.split('\n')
    fixed_lines = []
    
    for line in lines:
        original_line = line
        
        # Fix pattern: something(.Should().BeTrue())  -> something.Should().BeTrue()
        line = re.sub(r'(\w+)\(\.Should\(\)\.Be(\w+)\(\)\)', r'\1.Should().Be\2()', line)
        
        # Fix pattern: .First(.Should().BeTrue().property) -> .First().property.Should().BeTrue()
        line = re.sub(r'\.First\(\.Should\(\)\.BeTrue\(\)\.(\w+)\)', r'.First().\1.Should().BeTrue()', line)
        
        # Fix pattern: .First(.Should().BeFalse().property) -> .First().property.Should().BeFalse()
        line = re.sub(r'\.First\(\.Should\(\)\.BeFalse\(\)\.(\w+)\)', r'.First().\1.Should().BeFalse()', line)
        
        # Fix pattern: property(.Should().BeTrue()) -> property.Should().BeTrue()
        line = re.sub(r'(\w+)\(\.Should\(\)\.BeTrue\(\)\)', r'\1.Should().BeTrue()', line)
        line = re.sub(r'(\w+)\(\.Should\(\)\.BeFalse\(\)\)', r'\1.Should().BeFalse()', line)
        
        # Fix pattern: something.Contains("text".Should().BeTrue()) -> something.Should().Contain("text")
        line = re.sub(r'(\w+)\.Contains\("([^"]+)"\.Should\(\)\.BeTrue\(\)\)', r'\1.Should().Contain("\2")', line)
        line = re.sub(r'(\w+)\.Contains\(\'([^\']+)\'\.Should\(\)\.BeTrue\(\)\)', r"\1.Should().Contain('\2')", line)
        
        # Fix pattern with complex Contains checks
        line = re.sub(r'sql\.Contains\(([^)]+)\.Should\(\)\.BeTrue\(\)([^)]*)\)', r'sql.Should().Contain(\1\2)', line)
        
        # Fix pattern: comparison.Should().BeTrue() where comparison is like "x > y"
        match = re.match(r'^(\s*)(.+?)\s*>\s*(.+?)\.Should\(\)\.BeTrue\(\)(.*)$', line)
        if match:
            indent, left, right, rest = match.groups()
            line = f"{indent}{left}.Should().BeGreaterThan({right}){rest}"
        
        match = re.match(r'^(\s*)(.+?)\s*<\s*(.+?)\.Should\(\)\.BeTrue\(\)(.*)$', line)
        if match:
            indent, left, right, rest = match.groups()
            line = f"{indent}{left}.Should().BeLessThan({right}){rest}"
            
        # Fix lines ending with Should().BeTrue() where there's an expression before it
        # that looks like a boolean comparison
        if ' > ' in line and line.strip().endswith('.Should().BeTrue()'):
            # Extract the comparison
            pattern = r'(\s*)(.+?)\s+(.+?)\s*>\s*(.+?)\.Should\(\)\.BeTrue\(\)(.*)$'
            match = re.match(pattern, line)
            if match:
                indent, prefix, left, right, rest = match.groups()
                line = f"{indent}{prefix} {left}.Should().BeGreaterThan({right}){rest}"
        
        fixed_lines.append(line)
    
    return '\n'.join(fixed_lines)

def process_file(filepath):
    """Process a single file"""
    print(f"Processing {filepath}...")
    
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    original_content = content
    content = fix_assertion_issues(content)
    
    if content != original_content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"  Fixed assertions in {filepath}")
        return True
    else:
        print(f"  No changes needed in {filepath}")
        return False

def main():
    test_dir = '/mnt/e/work/github/crp/persistence-lib/UnitTest'
    
    # Find all test files
    test_files = []
    for root, dirs, files in os.walk(test_dir):
        for file in files:
            if file.endswith('Tests.cs'):
                test_files.append(os.path.join(root, file))
    
    files_modified = 0
    for filepath in test_files:
        if process_file(filepath):
            files_modified += 1
    
    print(f"\nTotal files modified: {files_modified}")

if __name__ == '__main__':
    main()