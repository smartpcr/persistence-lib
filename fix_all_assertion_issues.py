#!/usr/bin/env python3
import re
import os

def fix_assertion_issues(content):
    """Fix all remaining FluentAssertion syntax issues"""
    lines = content.split('\n')
    fixed_lines = []
    
    for line in lines:
        original_line = line
        
        # Fix pattern: ].Should().[method]() -> ].method.Should().Be[Value]()
        # e.g., results[0].Should().NotBeNull(); -> results[0].Should().NotBeNull();
        
        # Fix pattern with invalid prefix: results[0., results[1., etc.
        line = re.sub(r'results\[(\d+)\.', r'results[\1].', line)
        line = re.sub(r'entities\[(\d+)\.', r'entities[\1].', line)
        line = re.sub(r'items\[(\d+)\.', r'items[\1].', line)
        line = re.sub(r'list\[(\d+)\.', r'list[\1].', line)
        
        # Fix pattern: sql.Contains("text").Should().BeTrue() patterns that are within parens
        # This handles patterns in BaseEntityMapperAdvancedTests.cs
        line = re.sub(r'sql\.Should\(\)\.Contain\(([^)]+)\)\)', r'sql.Should().Contain(\1)', line)
        
        # Fix incomplete parentheses patterns
        line = re.sub(r'sql\.Should\(\)\.Contain\("([^"]+)"\)\);', r'sql.Should().Contain("\1");', line)
        
        fixed_lines.append(line)
    
    return '\n'.join(fixed_lines)

def process_file(filepath):
    """Process a single file"""
    if not os.path.exists(filepath):
        return False
        
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    original_content = content
    content = fix_assertion_issues(content)
    
    if content != original_content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"Fixed {filepath}")
        return True
    return False

# Fix specific files with known issues
files_to_fix = [
    '/mnt/e/work/github/crp/persistence-lib/UnitTest/BatchOperations/BatchOperationsTests.cs',
    '/mnt/e/work/github/crp/persistence-lib/UnitTest/BulkOperations/BulkOperationsTests.cs',
    '/mnt/e/work/github/crp/persistence-lib/UnitTest/CorePersistence/CrudOperationsTests.cs',
    '/mnt/e/work/github/crp/persistence-lib/UnitTest/Mappings/BaseEntityMapperTests.cs',
    '/mnt/e/work/github/crp/persistence-lib/UnitTest/ListOperations/ListOperationsTests.cs',
    '/mnt/e/work/github/crp/persistence-lib/UnitTest/Mappings/BaseEntityMapperValidationTests.cs',
    '/mnt/e/work/github/crp/persistence-lib/UnitTest/Mappings/BaseEntityMapperAdvancedTests.cs',
    '/mnt/e/work/github/crp/persistence-lib/UnitTest/Providers/SQLitePersistenceProviderAdvancedTests.cs'
]

for filepath in files_to_fix:
    process_file(filepath)