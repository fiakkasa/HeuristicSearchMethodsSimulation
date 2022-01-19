window.bindTspMapMarkerEvents = (dotNetHelper, parentId) => {
    try {
        document.querySelector('#' + parentId + ' > .js-plotly-plot').on(
            'plotly_click',
            (e) => _.attempt(() => {
                dotNetHelper.invokeMethodAsync('TspMapMarkerClick', _.get(e, ['points', '0', 'data', 'meta'], ''));
            })
        );
        return true;
    } catch (e) {
        return false;
    }
};