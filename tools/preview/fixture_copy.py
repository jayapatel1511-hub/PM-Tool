"""Display-copy compatibility for maintained fictional fixtures.

Only the fixture recipes and explicit fixture-copy cleanup use this helper.
It does not filter arbitrary application text or alter project/user identities.
"""


def natural_copy(value):
    if not isinstance(value, str):
        return value
    for prefix in (
        'Synthetic preview — ', 'Synthetic preview comment: ',
        'Synthetic preview action: ', 'Synthetic preview decision: ',
        'Synthetic preview mitigation: ', 'Synthetic preview trigger: ',
        'Synthetic preview finding: ', 'Synthetic preview: ',
        'Synthetic preview ', 'Synthetic ',
    ):
        if value.startswith(prefix):
            value = value[len(prefix):]
            break
    return (value.replace('the synthetic ', 'the ').replace('the fictional ', 'the ')
            .replace('fictional ', '').replace(' (fictional)', '')
            .replace(' (fictional change)', ''))
