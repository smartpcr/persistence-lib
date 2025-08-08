#!/usr/bin/env python3
import re
import os

def fix_all_issues(content):
    """Fix all remaining assertion issues"""
    lines = content.split('\n')
    fixed_lines = []
    
    for line in lines:
        # Fix pattern: e =.Should().BeGreaterThan(...) -> e => ...
        line = re.sub(r'(\w+) =\.Should\(\)\.BeGreaterThan\(([^)]+)\)', r'\1 => \2).Should().BeTrue(', line)
        
        # Fix pattern: [index. -> [index].
        line = re.sub(r'\[(\d+)\.', r'[\1].', line)
        
        fixed_lines.append(line)
    
    return '\n'.join(fixed_lines)

# Process files with known issues
files_to_fix = [
    '/mnt/e/work/github/crp/persistence-lib/UnitTest/BatchOperations/BatchOperationsTests.cs',
    '/mnt/e/work/github/crp/persistence-lib/UnitTest/BulkOperations/BulkOperationsTests.cs',
    '/mnt/e/work/github/crp/persistence-lib/UnitTest/CorePersistence/CrudOperationsTests.cs',
    '/mnt/e/work/github/crp/persistence-lib/UnitTest/ListOperations/ListOperationsTests.cs',
    '/mnt/e/work/github/crp/persistence-lib/UnitTest/Mappings/BaseEntityMapperTests.cs',
    '/mnt/e/work/github/crp/persistence-lib/UnitTest/Mappings/BaseEntityMapperValidationTests.cs',
    '/mnt/e/work/github/crp/persistence-lib/UnitTest/Providers/SQLitePersistenceProviderAdvancedTests.cs'
]

for filepath in files_to_fix:
    if os.path.exists(filepath):
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        original_content = content
        content = fix_all_issues(content)
        
        if content != original_content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"Fixed {filepath}")