#!/usr/bin/env python3
import re
import os

def fix_datetime_assertions(content):
    """Fix DateTime/DateTimeOffset assertions to use correct FluentAssertions methods"""
    lines = content.split('\n')
    fixed_lines = []
    
    for line in lines:
        # Fix DateTime comparisons
        line = re.sub(r'\.Should\(\)\.BeLessThanOrEqualTo\(', r'.Should().BeOnOrBefore(', line)
        line = re.sub(r'\.Should\(\)\.BeLessOrEqualTo\(', r'.Should().BeOnOrBefore(', line)
        line = re.sub(r'\.Should\(\)\.BeGreaterThanOrEqualTo\(', r'.Should().BeOnOrAfter(', line)
        line = re.sub(r'\.Should\(\)\.BeGreaterOrEqualTo\(', r'.Should().BeOnOrAfter(', line)
        line = re.sub(r'\.Should\(\)\.BeLessThan\(', r'.Should().BeBefore(', line)
        line = re.sub(r'\.Should\(\)\.BeGreaterThan\(', r'.Should().BeAfter(', line)
        
        # Fix cases where we have comparison operators with DateTime
        # Pattern: (dateTime1 > dateTime2).Should().BeTrue() -> dateTime1.Should().BeAfter(dateTime2)
        match = re.match(r'^(\s*)\((.+?)\s*>\s*(.+?)\)\.Should\(\)\.BeTrue\(\)', line)
        if match and ('Time' in line or 'Date' in line):
            indent, left, right = match.groups()
            line = f"{indent}{left}.Should().BeAfter({right})"
            
        match = re.match(r'^(\s*)\((.+?)\s*<\s*(.+?)\)\.Should\(\)\.BeTrue\(\)', line)
        if match and ('Time' in line or 'Date' in line):
            indent, left, right = match.groups()
            line = f"{indent}{left}.Should().BeBefore({right})"
            
        fixed_lines.append(line)
    
    return '\n'.join(fixed_lines)

# Process all test files
test_dir = '/mnt/e/work/github/crp/persistence-lib/UnitTest'

for root, dirs, files in os.walk(test_dir):
    for file in files:
        if file.endswith('.cs'):
            filepath = os.path.join(root, file)
            with open(filepath, 'r', encoding='utf-8') as f:
                content = f.read()
            
            original_content = content
            content = fix_datetime_assertions(content)
            
            if content != original_content:
                with open(filepath, 'w', encoding='utf-8') as f:
                    f.write(content)
                print(f"Fixed {filepath}")