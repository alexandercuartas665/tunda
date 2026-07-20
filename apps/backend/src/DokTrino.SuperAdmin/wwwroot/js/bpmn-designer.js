// Puente entre Blazor y bpmn-js. La libreria se sirve local desde
// wwwroot/lib/bpmn-js, asi que el disenador funciona sin salida a internet.
window.doktrinoBpmn = (() => {
    let modeler = null;

    const DIAGRAMA_VACIO = `<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI"
                  xmlns:dc="http://www.omg.org/spec/DD/20100524/DC"
                  id="Definitions_1"
                  targetNamespace="http://bpmn.io/schema/bpmn">
  <bpmn:process id="Process_1" isExecutable="true">
    <bpmn:startEvent id="StartEvent_1" name="Inicio" />
  </bpmn:process>
  <bpmndi:BPMNDiagram id="BPMNDiagram_1">
    <bpmndi:BPMNPlane id="BPMNPlane_1" bpmnElement="Process_1">
      <bpmndi:BPMNShape id="StartEvent_1_di" bpmnElement="StartEvent_1">
        <dc:Bounds x="180" y="160" width="36" height="36" />
      </bpmndi:BPMNShape>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>`;

    return {
        async iniciar(contenedorId, xml) {
            this.destruir();

            modeler = new BpmnJS({
                container: '#' + contenedorId,
                keyboard: { bindTo: document }
            });

            try {
                await modeler.importXML(xml && xml.trim().length > 0 ? xml : DIAGRAMA_VACIO);
                modeler.get('canvas').zoom('fit-viewport');
                return { ok: true };
            } catch (err) {
                // Un XML guardado corrupto no debe dejar el lienzo inutilizable:
                // se cae al diagrama vacio y se avisa.
                await modeler.importXML(DIAGRAMA_VACIO);
                modeler.get('canvas').zoom('fit-viewport');
                return { ok: false, error: err.message };
            }
        },

        async obtenerXml() {
            if (!modeler) { return null; }
            const { xml } = await modeler.saveXML({ format: true });
            return xml;
        },

        destruir() {
            if (modeler) {
                modeler.destroy();
                modeler = null;
            }
        }
    };
})();
