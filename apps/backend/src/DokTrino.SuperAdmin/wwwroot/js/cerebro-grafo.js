// Grafo de notas de un cerebro (vista tipo Obsidian) con Apache ECharts.
// ECharts (~1 MB) se carga bajo demanda la primera vez que se abre un grafo.
// Nodos = notas (los "fantasma" son enlaces a notas que aun no existen); aristas = [[enlaces]].
// Clic en un nodo -> dotnet.invokeMethodAsync('AbrirNodoGrafo', id, fantasma).
(function () {
    var cargando = null;
    var charts = new Map();

    function cargarEcharts() {
        if (window.echarts) { return Promise.resolve(); }
        if (cargando) { return cargando; }
        cargando = new Promise(function (ok, fallo) {
            var s = document.createElement('script');
            s.src = 'lib/echarts/echarts.min.js';
            s.onload = function () { ok(); };
            s.onerror = function () { cargando = null; fallo(new Error('No se pudo cargar ECharts')); };
            document.head.appendChild(s);
        });
        return cargando;
    }

    function colorPrimario() {
        var v = getComputedStyle(document.documentElement).getPropertyValue('--primary').trim();
        return v || '#2563eb';
    }

    function render(id, data, dotnet, opts) {
        opts = opts || {};
        return cargarEcharts().then(function () {
            var el = document.getElementById(id);
            if (!el) { return false; }
            var chart = charts.get(id);
            if (chart && chart.getDom() !== el) { chart.dispose(); chart = null; }
            if (!chart) {
                chart = window.echarts.init(el, null, { renderer: 'canvas' });
                charts.set(id, chart);
                var ro = new ResizeObserver(function () { chart.resize(); });
                ro.observe(el);
                chart.__ro = ro;
            }
            var primario = colorPrimario();
            var foco = opts.foco || null;
            var muchos = data.nodos.length > 80;
            var nodos = data.nodos.map(function (n) {
                var tam = Math.min(40, 8 + Math.sqrt(n.grado) * 5);
                var esFoco = n.id === foco;
                return {
                    id: n.id,
                    name: n.titulo,
                    value: n.grado,
                    fantasma: n.fantasma,
                    symbolSize: esFoco ? tam + 8 : tam,
                    category: n.fantasma ? 1 : 0,
                    itemStyle: esFoco ? { borderColor: '#1f2433', borderWidth: 3 } : undefined,
                    label: { show: !muchos || n.grado >= 3 || esFoco }
                };
            });
            var aristas = data.aristas.map(function (a) { return { source: a.origen, target: a.destino }; });
            chart.setOption({
                tooltip: {
                    formatter: function (p) {
                        if (p.dataType !== 'node') { return ''; }
                        var d = p.data;
                        var t = (d.name || '').replace(/[<>&]/g, function (c) { return { '<': '&lt;', '>': '&gt;', '&': '&amp;' }[c]; });
                        return '<b>' + t + '</b><br/>' + d.value + ' conexion(es)' + (d.fantasma ? '<br/><i>Nota aun no creada</i>' : '');
                    }
                },
                legend: opts.compacto ? undefined : [{ data: ['Nota', 'Sin crear'], bottom: 8, left: 12, textStyle: { color: '#59617a' } }],
                animationDurationUpdate: 600,
                series: [{
                    type: 'graph',
                    layout: 'force',
                    roam: true,
                    draggable: true,
                    scaleLimit: { min: 0.15, max: 6 },
                    data: nodos,
                    links: aristas,
                    categories: [
                        { name: 'Nota', itemStyle: { color: primario } },
                        { name: 'Sin crear', itemStyle: { color: '#b3bad0', opacity: 0.7 } }
                    ],
                    force: {
                        repulsion: muchos ? 90 : 180,
                        edgeLength: muchos ? [30, 90] : [60, 140],
                        gravity: muchos ? 0.12 : 0.08,
                        friction: 0.15
                    },
                    label: { position: 'right', fontSize: 11, color: '#37415c' },
                    lineStyle: { color: '#c3cadb', width: 1, opacity: 0.8, curveness: 0.05 },
                    emphasis: { focus: 'adjacency', lineStyle: { width: 2.5, color: primario }, label: { show: true, fontWeight: 'bold' } },
                    blur: { itemStyle: { opacity: 0.15 }, lineStyle: { opacity: 0.05 }, label: { show: false } }
                }]
            }, true);
            chart.off('click');
            chart.on('click', function (p) {
                if (p.dataType === 'node' && dotnet) {
                    dotnet.invokeMethodAsync('AbrirNodoGrafo', p.data.id, !!p.data.fantasma);
                }
            });
            return true;
        });
    }

    function liberar(id) {
        var chart = charts.get(id);
        if (!chart) { return; }
        if (chart.__ro) { chart.__ro.disconnect(); }
        chart.dispose();
        charts.delete(id);
    }

    // Lleva al final una lista con scroll (p. ej. los mensajes del chat de un agente).
    function bajar(id) {
        var e = document.getElementById(id);
        if (e) { e.scrollTop = e.scrollHeight; }
    }

    window.cerebroGrafo = { render: render, liberar: liberar, bajar: bajar };
})();
