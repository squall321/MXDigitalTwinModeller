"""
Simple YAML parser for specimen metadata
Supports basic key-value pairs and lists
"""

def parse_yaml(file_path):
    """
    Parse a simple YAML file and return a dictionary

    Supports:
    - key: value pairs
    - lists (key: - item)
    - comments (# ...)

    Args:
        file_path: Path to YAML file

    Returns:
        dict: Parsed YAML data
    """
    data = {}
    current_list_key = None
    current_list = []

    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            for line in f:
                line = line.rstrip('\n\r')

                # Skip empty lines
                if not line.strip():
                    continue

                # Skip comments
                if line.strip().startswith('#'):
                    continue

                # List item
                if line.strip().startswith('- '):
                    if current_list_key is not None:
                        item = line.strip()[2:].strip()
                        # Remove quotes if present
                        if item.startswith('"') and item.endswith('"'):
                            item = item[1:-1]
                        current_list.append(item)
                    continue

                # Key-value pair
                if ': ' in line or ':\t' in line or line.endswith(':'):
                    # Close previous list if any
                    if current_list_key is not None and current_list:
                        data[current_list_key] = current_list
                        current_list_key = None
                        current_list = []

                    # Parse key-value
                    if ': ' in line:
                        key, value = line.split(': ', 1)
                    elif ':\t' in line:
                        key, value = line.split(':\t', 1)
                    else:  # line.endswith(':')
                        key = line.rstrip(':')
                        value = ''

                    key = key.strip()
                    value = value.strip()

                    # Check if this starts a list
                    if not value:
                        current_list_key = key
                        current_list = []
                        continue

                    # Remove quotes
                    if value.startswith('"') and value.endswith('"'):
                        value = value[1:-1]

                    # Type conversion
                    if value.lower() == 'null':
                        value = None
                    elif value.lower() == 'true':
                        value = True
                    elif value.lower() == 'false':
                        value = False
                    else:
                        # Try to convert to number
                        try:
                            if '.' in value:
                                value = float(value)
                            else:
                                value = int(value)
                        except ValueError:
                            pass  # Keep as string

                    data[key] = value

        # Close final list if any
        if current_list_key is not None and current_list:
            data[current_list_key] = current_list

        return data

    except Exception as e:
        print(f"[YAML Parser] Error parsing {file_path}: {e}")
        return {}


def get_specimen_info_from_yaml(yaml_data):
    """
    Extract specimen information from parsed YAML data

    Args:
        yaml_data: Dictionary from parse_yaml()

    Returns:
        dict: Specimen info in expected format
    """
    if not yaml_data:
        return None

    try:
        # Support both PascalCase_mm and lowercase_mm keys
        gauge_length = yaml_data.get('GaugeLength_mm', yaml_data.get('gauge_length_mm', 0))
        gauge_width = yaml_data.get('GaugeWidth_mm', yaml_data.get('gauge_width_mm', 0))
        thickness = yaml_data.get('Thickness_mm', yaml_data.get('thickness_mm', 0))
        grip_length = yaml_data.get('GripLength_mm', yaml_data.get('grip_length_mm', 0))
        grip_width = yaml_data.get('GripWidth_mm', yaml_data.get('grip_width_mm', 0))
        total_length = yaml_data.get('TotalLength_mm', yaml_data.get('total_length_mm', 0))
        specimen_type = yaml_data.get('SpecimenType', yaml_data.get('specimen_type', 'Unknown'))
        created_date = yaml_data.get('CreatedDate', yaml_data.get('created_date', ''))

        return {
            'GaugeLength': gauge_length,
            'GaugeWidth': gauge_width,
            'Thickness': thickness,
            'SpecimenType': specimen_type,
            'CrossSectionArea': gauge_width * thickness,
            'GripLength': grip_length,
            'GripWidth': grip_width,
            'TotalLength': total_length,
            'NamedSelections': yaml_data.get('NamedSelections', yaml_data.get('named_selections', [])),
            'CreatedDate': created_date,
            'DocumentPath': yaml_data.get('DocumentPath', yaml_data.get('document_path', ''))
        }
    except Exception as e:
        print(f"[Specimen Info] Error extracting info: {e}")
        return None
