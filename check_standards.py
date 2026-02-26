import os
import re
from pathlib import Path

# Paths to check
infra_dir = Path(r"c:\Users\user\Documents\Unity\Scaffold\Assets\Scripts\Infra")
utility_dir = Path(r"c:\Users\user\Documents\Unity\Scaffold\Assets\Scripts\Utility")

files_to_check = list(infra_dir.rglob("*.cs")) + list(utility_dir.rglob("*.cs"))

violations = []

def report(file, rule, message):
    violations.append(f"{file.relative_to(infra_dir.parent.parent.parent)}: [{rule}] {message}")

for file in files_to_check:
    try:
        content = file.read_text(encoding='utf-8')
    except:
        continue
    
    # 1. Namespace checks
    rel_path = file.relative_to(infra_dir.parent) 
    parts = list(rel_path.parts)
    if parts[0] in ['Game', 'Infra', 'Utility']:
        parts = parts[1:]
    
    expected_namespace_base = "Scaffold"
    if len(parts) > 0:
        module_name = parts[0]
        expected_namespace_base += f".{module_name}"
        if len(parts) > 1 and parts[1] != 'Runtime' and parts[1] != 'Editor':
            # E.g. Containers/Runtime -> Scaffold.Containers
            # Containers/Editor -> Scaffold.Containers.Editor
            pass
        if len(parts) > 1 and parts[1] != 'Runtime':
            expected_namespace_base += f".{parts[1]}"
            
    ns_match = re.search(r'^namespace\s+([\w\.]+)', content, re.MULTILINE)
    if not ns_match:
        report(file, "Namespaces", "No namespace declared.")
    else:
        actual_ns = ns_match.group(1)
        if not actual_ns.startswith(expected_namespace_base):
            if not (actual_ns == expected_namespace_base or actual_ns.startswith(expected_namespace_base + ".")):
                report(file, "Namespaces", f"Expected namespace to start with '{expected_namespace_base}', but was '{actual_ns}'")

    # 2. Private fields no _ or m_ prefix, camelCase
    # Need to avoid matching methods
    private_fields = re.finditer(r'^\s*private\s+(?:readonly\s+)?(?:[\w<>,\[\]\?]+\s+)([_a-zA-Z0-9]+)\s*[;=]', content, re.MULTILINE)
    for m in private_fields:
        name = m.group(1)
        if name.startswith('_') or name.startswith('m_') or name[0].isupper():
            report(file, "Naming conventions", f"Private field '{name}' must be camelCase without _ or m_ prefix.")
            
    # 3. Public fields/Properties PascalCase
    public_props = re.finditer(r'^\s*public\s+(?:readonly\s+|virtual\s+|override\s+|abstract\s+|static\s+)*([\w<>,\[\]\?]+)\s+([a-z_][a-zA-Z0-9]*)\s*[{;=]', content, re.MULTILINE)
    for m in public_props:
        prop_type = m.group(1)
        name = m.group(2)
        if name not in ['class', 'interface', 'struct', 'record', 'delegate', 'enum']:
            if (prop_type == 'Transform' and name == 'transform') or (prop_type == 'GameObject' and name == 'gameObject'):
                pass
            else:
                report(file, "Naming conventions", f"Public field/property '{name}' must be PascalCase.")
            
    # 4. Interface starts with I
    interfaces = re.finditer(r'^\s*(?:public|internal)\s+interface\s+([^I\s][\w]*)', content, re.MULTILINE)
    for m in interfaces:
        report(file, "Naming conventions", f"Interface '{m.group(1)}' must start with 'I'.")
        
    # 5. Method names PascalCase
    methods = re.finditer(r'^\s*(?:public|private|protected|internal)\s+(?:(?:virtual|abstract|override|static|async|unsafe)\s+)*(?:[\w<>,\[\]\?]+\s+)([a-z_][\w]*)\s*\(', content, re.MULTILINE)
    for m in methods:
        name = m.group(1)
        if name not in ['if', 'for', 'while', 'switch', 'return', 'catch', 'typeof', 'sizeof', 'new']:
            report(file, "Naming conventions", f"Method '{name}' must be PascalCase.")
            
    # 6. Method expression bodies
    expr_body_methods = re.finditer(r'^\s*(?:public|private|protected|internal)\s+(?:(?:virtual|abstract|override|static|async)\s+)*(?:[\w<>,\[\]\?]+\s+)\w+\s*\([^)]*\)\s*=>', content, re.MULTILINE)
    for m in expr_body_methods:
        report(file, "Method body syntax", "Expression-body '=>' syntax used for a method.")
        
    # 7. Record custom body
    records = re.finditer(r'^\s*(?:public|internal)\s+record\s+(?:class\s+|struct\s+)?\w+\s*\{', content, re.MULTILINE)
    for m in records:
        report(file, "Immutable records", "Record using separate body '{ ... }' instead of inline constructor.")
        
    # 8. Nested calls - rough check
    nested_calls = re.finditer(r'\b(\w+)\s*\([^;{}]*\b(\w+)\s*\([^;{}]*\)', content)
    # This might have too many false positives. Skip.

    # 9. Multiple classes per file
    classes = re.findall(r'^\s*(?:public|internal)\s+(?:abstract\s+|sealed\s+|static\s+|partial\s+)?(?:class|struct)\s+\w+', content, re.MULTILINE)
    if len(classes) > 1:
        report(file, "One class per file", f"Found {len(classes)} public/internal classes/structs.")
        
    # 10. Multi-line method signatures
    multiline_sigs = re.finditer(r'^\s*(?:public|private|protected|internal)\s+(?:(?:virtual|abstract|override|static|async)\s+)*(?:[\w<>,\[\]\?]+\s+)\w+\s*\([^)]*,\s*\n', content, re.MULTILINE)
    for m in multiline_sigs:
        report(file, "Line breaks", "Multi-line method signature detected.")

for v in violations:
    print(v)

if not violations:
    print("No obvious violations found by the automated script.")
print(f"Checked {len(files_to_check)} files.")
