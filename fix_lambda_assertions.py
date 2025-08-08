#!/usr/bin/env python3
import re
import os

def fix_lambda_assertions(content):
    """Fix lambda expression issues in assertions"""
    lines = content.split('\n')
    fixed_lines = []
    
    for line in lines:
        # Fix pattern: .All(r =.Should().BeGreaterThan(...)) -> .All(r => ...).Should().BeTrue()
        # Match and fix patterns like: results.All(r =.Should().BeGreaterThan(r.Version == 1))
        line = re.sub(r'\.All\((\w+) =\.Should\(\)\.BeGreaterThan\(([^)]+)\)\)', r'.All(\1 => \2).Should().BeTrue()', line)
        
        # Fix patterns like: results[0. -> results[0].
        line = re.sub(r'results\[(\d+)\.', r'results[\1].', line)
        line = re.sub(r'entities\[(\d+)\.', r'entities[\1].', line)
        line = re.sub(r'items\[(\d+)\.', r'items[\1].', line)
        line = re.sub(r'list\[(\d+)\.', r'list[\1].', line)
        line = re.sub(r'entries\[(\d+)\.', r'entries[\1].', line)
        
        fixed_lines.append(line)
    
    return '\n'.join(fixed_lines)

# Process specific files with known issues
test_dir = '/mnt/e/work/github/crp/persistence-lib/UnitTest'

for root, dirs, files in os.walk(test_dir):
    for file in files:
        if file.endswith('.cs'):
            filepath = os.path.join(root, file)
            with open(filepath, 'r', encoding='utf-8') as f:
                content = f.read()
            
            original_content = content
            content = fix_lambda_assertions(content)
            
            if content != original_content:
                with open(filepath, 'w', encoding='utf-8') as f:
                    f.write(content)
                print(f"Fixed {filepath}")