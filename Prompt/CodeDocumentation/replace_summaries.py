import os
import sys
import re

def extract_summary(cs_file, class_name):
    try:
        with open(cs_file, 'r', encoding='utf-8') as f:
            content = f.read()
            
        # Try to find class definition
        class_pattern = re.compile(r'(///\s*<summary>.*?///\s*</summary>).*?(class|interface|struct)\s+' + re.escape(class_name) + r'\b', re.DOTALL)
        match = class_pattern.search(content)
        if match:
            summary_block = match.group(1)
            lines = summary_block.split('\n')
            clean_lines = []
            for line in lines:
                line = line.strip()
                if line.startswith('///'):
                    line = line[3:].strip()
                if line and not line.startswith('<summary>') and not line.startswith('</summary>'):
                    clean_lines.append(line)
            return ' '.join(clean_lines).strip()
    except Exception:
        pass
    return "No description provided."

def process_dir(directory):
    for root, _, files in os.walk(directory):
        for file in files:
            if file.endswith('Read.md'):
                md_path = os.path.join(root, file)
                
                with open(md_path, 'r', encoding='utf-8') as f:
                    content = f.read()
                
                if '[LLM_GENERATED_DESCRIPTION_HERE]' not in content:
                    continue
                    
                lines = content.split('\n')
                modified_lines = []
                current_class = None
                
                for line in lines:
                    match = re.match(r'^###\s+`([\w<>\[\]]+)`', line)
                    if match:
                        full_class = match.group(1)
                        current_class = re.sub(r'<.*>', '', full_class)
                    
                    if '- **Description**: [LLM_GENERATED_DESCRIPTION_HERE]' in line and current_class:
                        # Find corresponding .cs file
                        cs_file_path = os.path.join(root, current_class + '.cs')
                        summary = extract_summary(cs_file_path, current_class)
                        if summary == "No description provided." and not os.path.exists(cs_file_path):
                            # Search other cs files in the same dir
                            for cs_f in os.listdir(root):
                                if cs_f.endswith('.cs'):
                                    summary = extract_summary(os.path.join(root, cs_f), current_class)
                                    if summary != "No description provided.":
                                        break
                                        
                        line = line.replace('[LLM_GENERATED_DESCRIPTION_HERE]', summary)
                    modified_lines.append(line)
                
                with open(md_path, 'w', encoding='utf-8') as f:
                    f.write('\n'.join(modified_lines))
                print(f"Updated {md_path}")

if __name__ == "__main__":
    if len(sys.argv) > 1:
        for arg in sys.argv[1:]:
            process_dir(arg)
    else:
        print("Usage: python replace_summaries.py <directory_path>...")
        sys.exit(1)
