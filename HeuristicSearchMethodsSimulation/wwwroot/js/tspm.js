window.bindTspMapMarkerEvents = (dotNetHelper, parentId) => {
    try {
        var el = document.querySelector('#' + parentId + ' > .js-plotly-plot');

        if (el.getAttribute('parent-id') === parentId) return;

        el.setAttribute('parent-id', parentId);
        el.on(
            'plotly_click',
            (e) => _.attempt(async () => {
                await dotNetHelper.invokeMethodAsync('TspMapMarkerClick', _.get(e, ['points', '0', 'data', 'meta'], ''));
            })
        );
        return true;
    } catch (e) {
        return false;
    }
};