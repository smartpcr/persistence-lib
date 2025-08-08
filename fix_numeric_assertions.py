#!/usr/bin/env python3
import re
import os

def fix_numeric_assertions(content):
    """Fix numeric assertions that incorrectly use DateTime methods"""
    lines = content.split('\n')
    fixed_lines = []
    
    for line in lines:
        # If the line doesn't contain DateTime/DateTimeOffset/Time/Date in context, 
        # fix numeric assertions
        if not ('DateTime' in line or 'Time' in line or 'Date' in line):
            # Fix numeric comparisons that incorrectly use DateTime methods
            line = re.sub(r'\.Should\(\)\.BeAfter\(', r'.Should().BeGreaterThan(', line)
            line = re.sub(r'\.Should\(\)\.BeBefore\(', r'.Should().BeLessThan(', line)
            line = re.sub(r'\.Should\(\)\.BeOnOrAfter\(', r'.Should().BeGreaterThanOrEqualTo(', line)
            line = re.sub(r'\.Should\(\)\.BeOnOrBefore\(', r'.Should().BeLessThanOrEqualTo(', line)
        
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
            content = fix_numeric_assertions(content)
            
            if content != original_content:
                with open(filepath, 'w', encoding='utf-8') as f:
                    f.write(content)
                print(f"Fixed {filepath}")