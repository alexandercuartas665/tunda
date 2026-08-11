// Sincroniza una barra de scroll horizontal superior con el contenedor real de la
// tabla (inferior), para que la Tabla de Retencion Documental se pueda desplazar
// desde arriba o desde abajo. Blazor llama tablaDobleScroll.init con los dos
// elementos (ElementReference se recibe como el nodo DOM real).
window.tablaDobleScroll = {
    init: function (topEl, bottomEl) {
        if (!topEl || !bottomEl) { return; }
        var inner = topEl.firstElementChild;
        var tabla = bottomEl.querySelector('table');
        var ancho = tabla ? tabla.scrollWidth : bottomEl.scrollWidth;
        if (inner) { inner.style.width = ancho + 'px'; }

        // Evita el bucle de eventos entre ambas barras.
        if (topEl.__wired) { return; }
        topEl.__wired = true;
        var lock = false;
        topEl.addEventListener('scroll', function () {
            if (lock) { return; }
            lock = true; bottomEl.scrollLeft = topEl.scrollLeft; lock = false;
        });
        bottomEl.addEventListener('scroll', function () {
            if (lock) { return; }
            lock = true; topEl.scrollLeft = bottomEl.scrollLeft; lock = false;
        });
    }
};
