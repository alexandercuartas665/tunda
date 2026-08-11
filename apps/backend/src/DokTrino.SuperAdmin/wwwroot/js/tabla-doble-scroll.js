// Sincroniza una barra de scroll horizontal superior con el contenedor real de la
// tabla (inferior), para que la Tabla de Retencion Documental se pueda desplazar
// desde arriba o desde abajo. Ademas ajusta el "top" de la fila de hojas del
// encabezado a la altura real de la fila de grupos, para que el encabezado sticky
// de 2 filas no se solape al hacer scroll vertical. Blazor llama
// tablaDobleScroll.init con los dos elementos (ElementReference -> nodo DOM real).
window.tablaDobleScroll = {
    init: function (topEl, bottomEl) {
        if (!topEl || !bottomEl) { return; }
        var tabla = bottomEl.querySelector('table');
        var inner = topEl.firstElementChild;
        var ancho = tabla ? tabla.scrollWidth : bottomEl.scrollWidth;
        if (inner) { inner.style.width = ancho + 'px'; }

        // Encabezado sticky de 2 filas: la fila de hojas se pega justo debajo de la
        // fila de grupos (su altura real), no en un valor fijo que la solape.
        if (tabla && tabla.tHead && tabla.tHead.rows.length >= 2) {
            var grpH = tabla.tHead.rows[0].getBoundingClientRect().height;
            if (grpH > 0) {
                var hojas = tabla.tHead.rows[1].cells;
                for (var k = 0; k < hojas.length; k++) { hojas[k].style.top = grpH + 'px'; }
            }
        }

        // Evita el bucle de eventos entre ambas barras (se cablea una sola vez).
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
