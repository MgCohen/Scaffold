import os
import re
import hashlib

def compute_dir_hash(cs_files):
    hasher = hashlib.md5()
    for f in sorted(cs_files):
        with open(f, 'rb') as file:
            hasher.update(file.read())
    return hasher.hexdigest()

def gather_cs_info(filepath):
    content = ""
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    namespace = re.search(r'namespace\s+([^\s{]+)', content)
    ns = namespace.group(1) if namespace else "Global"

    class_match = re.search(r'(?:public|internal|private|protected)?\s*(?:static|sealed|abstract|partial)?\s+(class|interface|struct)\s+(\w+)(?:\s*:\s*([^{]+))?', content)

    if not class_match:
        return None

    type_name = class_match.group(1)
    name = class_match.group(2)
    bases = class_match.group(3)
    base_list = [b.strip() for b in bases.split(',')] if bases else []

    methods = re.findall(r'(?:public|private|protected|internal)\s+(?:virtual|override|static|abstract)?\s*[\w<>\[\]]+\s+(\w+)\s*\(', content)
    properties = re.findall(r'(?:public|private|protected|internal)\s+(?:virtual|override|static|abstract)?\s*[\w<>\[\]]+\s+(\w+)\s*\{\s*get', content)

    # Cleanup basic C# keywords from methods
    invalid_methods = {'if', 'for', 'foreach', 'while', 'switch', 'catch', 'using', 'lock', 'typeof', 'sizeof', 'throw'}
    cleaned_methods = [m for m in methods if m not in invalid_methods]

    return {
        'filepath': filepath,
        'namespace': ns,
        'type': type_name,
        'name': name,
        'bases': base_list,
        'methods': list(set(cleaned_methods)),
        'properties': list(set(properties)),
        'summary': "[LLM_GENERATED_DESCRIPTION_HERE]"
    }

def process_directory(dir_path, root_dir):
    local_name = os.path.basename(dir_path)
    if not local_name:
        local_name = "Root"

    cs_files = []
    sub_dirs = []
    for item in os.listdir(dir_path):
        item_path = os.path.join(dir_path, item)
        if os.path.isfile(item_path) and item_path.endswith('.cs'):
            cs_files.append(item_path)
        elif os.path.isdir(item_path) and item not in ('obj', 'bin', '.git', '.idea'):
            sub_dirs.append(item)

    if not cs_files and not sub_dirs:
        return

    dir_hash = compute_dir_hash(cs_files) if cs_files else "EmptyDir"
    output_filename = f"{local_name}Read.md"
    output_path = os.path.join(dir_path, output_filename)

    # Check if we should skip generating because the hash hasn't changed
    if os.path.exists(output_path):
        with open(output_path, 'r', encoding='utf-8') as f:
            first_line = f.readline()
            if first_line.startswith('<!-- hash:') and dir_hash in first_line:
                print(f"Skipped {output_path}, hash unchanged.")
                return

    infos = []
    for f in cs_files:
        info = gather_cs_info(f)
        if info:
            infos.append(info)

    print(f"Generating {output_path}...")
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write(f"<!-- hash: {dir_hash} -->\n")
        f.write(f"# {local_name} Documentation\n\n")
        rel_path = dir_path.replace(root_dir, '')
        if not rel_path:
            rel_path = "/"
            
        f.write(f"This document details the purpose and relations of the components in `{rel_path}`.\n\n")
        
        if sub_dirs:
            f.write("## Sub-Modules\n\n")
            for sub in sorted(sub_dirs):
                f.write(f"- [{sub}]({sub}/{sub}Read.md)\n")
            f.write("\n")

        if infos:
            f.write("## Component Overview\n\n")
            for info in infos:
                f.write(f"### `{info['name']}` ({info['type']})\n")
                f.write(f"- **Description**: {info['summary']}\n")
                f.write(f"- **Namespace**: `{info['namespace']}`\n")
                if info['bases']:
                    f.write(f"- **Inherits/Implements**: {', '.join(['`' + b + '`' for b in info['bases']])}\n")
                if info['properties']:
                    f.write(f"- **Properties**: {', '.join(['`' + p + '`' for p in info['properties']])}\n")
                if info['methods']:
                    f.write(f"- **Methods**: {', '.join(['`' + m + '`' for m in info['methods']])}\n")
                f.write("\n")

            f.write("## Dependency & Behavior Schema\n\n")
            f.write("```mermaid\n")
            f.write("graph TD\n")
            for info in infos:
                f.write(f"    {info['name']}[{info['name']}]\n")
                for base in info['bases']:
                    clean_base = re.sub(r'<.*>', '', base).split('.')[-1]
                    if len(clean_base) > 1:
                        f.write(f"    {info['name']} -->|inherits/implements| {clean_base}\n")
            f.write("```\n\n")

        if dir_path != root_dir:
            parent_name = os.path.basename(os.path.dirname(dir_path))
            f.write(f"\n[Back to Parent](../{parent_name}Read.md)\n")

def walk_and_process(base_dir):
    for root, dirs, files in os.walk(base_dir):
        for d in ['obj', 'bin', '.git', '.idea']:
            if d in dirs:
                dirs.remove(d)
        
        process_directory(root, base_dir)

base_scaffold = "/Users/leonardosilva/Documents/MatheusCohen/Scaffold/GameModule"
project_root = os.path.join(base_scaffold, "Project")
dto_root = os.path.join(base_scaffold, "GameModuleDTO")

walk_and_process(project_root)
walk_and_process(dto_root)

# Root read.md
root_md = os.path.join(base_scaffold, "read.md")
with open(root_md, 'w', encoding='utf-8') as f:
    f.write("<!-- hash: root -->\n")
    f.write("# GameModule Root Documentation\n\n")
    f.write("## Introduction\n")
    f.write("This is the main entry point for the GameModule repository. The architecture is split primarily into two domains: **Project** and **GameModuleDTO**.\n\n")
    f.write("## Scalability and Architecture\n")
    f.write("- **GameModuleDTO**: Holds the data transfer objects, keys, requests, and interfaces. It ensures a decoupled standard format that can scale horizontally without affecting logic.\n")
    f.write("- **Project**: Contains the core logic, implementations of module systems, authentication, state fetchers, and signal management.\n\n")
    f.write("## Interactive Systems\n")
    f.write("```mermaid\n")
    f.write("graph TD\n")
    f.write("    Project[Core Game Systems] <--> DTO[Data Transfer Objects]\n")
    f.write("    DTO --> ClientRequests[Client Side Requests]\n")
    f.write("    Project --> ServerResponse[Server Handlers]\n")
    f.write("```\n\n")
    f.write("## Navigation\n")
    f.write("- [Project Architecture](Project/ProjectRead.md)\n")
    f.write("- [GameModuleDTO Overview](GameModuleDTO/GameModuleDTORead.md)\n")

print("Documentation generated successfully.")
