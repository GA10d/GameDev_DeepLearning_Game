"""Validate teaching prerequisites and matching design artifacts. No packages needed."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
docs = root / 'docs'
data = json.loads((docs / 'levels.json').read_text(encoding='utf-8'))
levels = {level['id']: level for level in data['levels']}
modules = {module['id']: module for module in data['modules']}
assert len(levels) == len(data['levels']), 'Duplicate level ids'
assert len(modules) == len(data['modules']), 'Duplicate module ids'
visiting, checked = set(), {}

def ancestors(key):
    assert key in levels, f'Missing level: {key}'
    assert key not in visiting, f'Cycle: {key}'
    if key in checked:
        return checked[key]
    visiting.add(key)
    result = {key}
    for dep in levels[key]['deps']:
        if levels[key]['type'] != '支线':
            assert levels[dep]['type'] != '支线', f'Optional prerequisite blocks main: {key}'
        result.update(ancestors(dep))
    visiting.remove(key)
    checked[key] = result
    return result

for module in modules.values():
    assert module['intro_level'] in levels, module['id']
    assert module['card'] and module['category'], f'Missing module description: {module["id"]}'
for level in levels.values():
    path = ancestors(level['id'])
    expected = {m['id'] for m in modules.values() if m['intro_level'] == level['id']}
    assert set(level['introduces']) == expected, f'Introduction mismatch: {level["id"]}'
    for module_id in level['uses']:
        assert module_id in modules, f'Unknown module: {level["id"]} {module_id}'
        assert modules[module_id]['intro_level'] in path, f'Untaught module: {level["id"]} {module_id}'
    assert all(level['teaching'].get(x) for x in ('text', 'demo', 'check')), level['id']
    assert all(level['visualization'].get(x) for x in ('main', 'interaction', 'failure', 'completion')), level['id']
main = {x['id'] for x in levels.values() if x['type'] != '支线'}
assert ancestors('G04') == main, 'Finale does not cover all required levels'
assert data['audit']['modules'] == len(modules), 'Stale module audit count'
assert data['audit']['levels'] == len(levels), 'Stale level audit count'

html = (docs / 'level-map.html').read_text(encoding='utf-8')
embedded = re.search(r'<script id="level-data" type="application/json">(.*?)</script>', html, re.S)
assert embedded and json.loads(embedded.group(1)) == data, 'Map contains stale level data'
texts = {}
for name in ('level-design.md', 'module-introductions.md', 'visualization-design.md'):
    text = (docs / name).read_text(encoding='utf-8')
    texts[name] = text
    assert '\ufffd' not in text, f'Invalid text character: {name}'
    for level in levels.values():
        assert f'#### {level["id"]} {level["name"]}' in text, f'Missing level: {name} {level["id"]}'
for module in modules.values():
    assert module['card'] in texts['module-introductions.md'], f'Stale module card: {module["id"]}'
for level in levels.values():
    for key in ('text', 'demo', 'check'):
        assert level['teaching'][key] in texts['module-introductions.md'], f'Stale teaching: {level["id"]} {key}'
    for value in level['visualization'].values():
        assert value in texts['visualization-design.md'], f'Stale visualization: {level["id"]}'

print(json.dumps(dict(levels=len(levels), required=len(main), optional=len(levels)-len(main),
                      modules=len(modules), missing_introductions=0, inaccessible_modules=0,
                      visualization_specs=len(levels), map_data_matches=True), ensure_ascii=True))
