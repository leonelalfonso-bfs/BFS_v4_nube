"""Carga datos de ejemplo en una instalación de prueba para sacar las capturas de los manuales."""
import json
import sys
import urllib.request

URL = sys.argv[1]


def cuit(base):
    """CUIT válido a partir de prefijo+DNI (10 dígitos)."""
    pesos = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2]
    s = sum(int(d) * p for d, p in zip(base, pesos))
    v = 11 - s % 11
    v = 0 if v == 11 else (9 if v == 10 else v)
    return f"{base[:2]}-{base[2:]}-{v}"


def llamar(metodo, ruta, cuerpo=None, token=None):
    req = urllib.request.Request(URL + ruta, method=metodo,
                                 data=json.dumps(cuerpo).encode() if cuerpo is not None else None,
                                 headers={"Content-Type": "application/json", **({"Authorization": "Bearer " + token} if token else {})})
    try:
        with urllib.request.urlopen(req) as r:
            t = r.read().decode()
            return json.loads(t) if t and t[0] in "[{" else t
    except urllib.error.HTTPError as e:
        raise SystemExit(f"{metodo} {ruta}: {e.code} {e.read().decode()}")


def login(u, p):
    return llamar("POST", "/api/auth/login", {"email": u, "password": p})["token"]


bfs = login("admin", "BFS202%")
llamar("PUT", "/api/config", {
    "cliente_nombre": "Acopio Los Aromos SA", "aduana": "true", "tara_conocida": "true", "tara_pallets": "true",
    "exigir_estable": "false", "ingreso_manual": "true", "ticket_titulo": "Comprobante de pesaje",
    "ticket_pie": "Gracias por su visita"}, bfs)

empresas = llamar("GET", "/api/empresas", token=bfs)
e1, e2 = empresas[0]["id"], empresas[1]["id"]
llamar("PUT", f"/api/empresas/{e1}", {"nombre": "Acopio Los Aromos SA", "cuit": cuit("3071234567"), "direccion": "Ruta 34 Km 245",
                                     "localidad": "San Lorenzo", "provincia": "Santa Fe", "telefono": "0341 555-0101",
                                     "email": "balanza@losaromos.com.ar"}, bfs)
llamar("PUT", f"/api/empresas/{e2}", {"nombre": "Los Aromos Logística SRL", "cuit": cuit("3071765432"), "direccion": "Ruta 34 Km 245",
                                     "localidad": "San Lorenzo", "provincia": "Santa Fe"}, bfs)

# Segunda balanza (simulada) para mostrar los dos displays.
balanzas = llamar("GET", "/api/balanzas", token=bfs)
if len(balanzas) < 2:
    b1 = balanzas[0]["balanza"]
    llamar("PUT", f"/api/balanzas/{b1['id']}", {**{k: b1[k] for k in ["modo", "puerto", "modelo", "baudios", "dataBits", "paridad", "stopBits", "protocolo", "extraccion", "orden", "activa"]}, "nombre": "Balanza Norte"}, bfs)
    llamar("POST", "/api/balanzas", {"nombre": "Balanza Sur", "modo": "sim", "baudios": 9600, "dataBits": 8, "paridad": "None",
                                     "stopBits": 1, "protocolo": "Line", "orden": 2}, bfs)

for c in [("Cooperativa Agrícola del Sur", "3070111222"), ("Molinos Río Paraná SA", "3070333444"), ("Estancia La Esperanza", "2028555666")]:
    llamar("POST", "/api/catalogos/clientes", {"razonSocial": c[0], "cuit": cuit(c[1])}, bfs)
for t in [("Transportes El Ceibo", "3070777888", False, None), ("Logística Paraná SRL", "3070999000", True, "ATA-0457"), ("Fletes Don Luis", "2030111222", False, None)]:
    llamar("POST", "/api/catalogos/transportes", {"razonSocial": t[0], "cuit": cuit(t[1]), "esAta": t[2], "codigoAta": t[3]}, bfs)
for p in [("Soja", "101"), ("Maíz", "102"), ("Trigo", "103"), ("Girasol", "104")]:
    llamar("POST", "/api/catalogos/productos", {"nombre": p[0], "codigo": p[1]}, bfs)
llamar("POST", "/api/catalogos/exportadores", {"razonSocial": "Granos del Litoral SA", "cuit": cuit("3072222333")}, bfs)
llamar("POST", "/api/catalogos/taras", {"identificador": "AE123CD", "kilogramos": 14800, "descripcion": "Camión batea"}, bfs)

llamar("PUT", "/api/planta/aduana", {"codigo": "052", "lot": "LOT-0331", "certificado1": "INTI-2026-0458", "certificado1Vence": "2027-06-30"}, bfs)

# Usuarios del cliente.
llamar("POST", "/api/usuarios", {"email": "jefe@losaromos.com.ar", "password": "jefe2026", "nombre": "Marcela Gómez", "rol": "admin"}, bfs)
llamar("POST", "/api/usuarios", {"email": "supervisor", "password": "super2026", "nombre": "Carlos Ruiz", "rol": "supervisor"}, bfs)
llamar("POST", "/api/usuarios", {"email": "porteria1", "password": "porteria1", "nombre": "Portería turno mañana", "rol": "operador"}, bfs)

sup = login("supervisor", "super2026")
pesadas = [
    ("AB123CD", "AC456EF", "Juan Pérez", "28555666", "Cooperativa Agrícola del Sur", "Transportes El Ceibo", "Soja", "Pergamino", "", "R0003-00012345", 44240, 15020),
    ("AF987GH", None, "Miguel Sosa", "30111222", "Molinos Río Paraná SA", "Fletes Don Luis", "Maíz", "Venado Tuerto", "", "R0001-00004567", 41380, 14650),
    ("AD456JK", "AD789LM", "Ramón Díaz", "25444333", "Estancia La Esperanza", "Transportes El Ceibo", "Trigo", "Casilda", "", "R0002-00000881", 39910, 15200),
    ("AA321BC", None, "Luis Benítez", "27666555", "Cooperativa Agrícola del Sur", "Fletes Don Luis", "Girasol", "Rufino", "", "", 35770, None),
    ("AG654NP", "AG111QR", "Pedro Acosta", "31222333", "Molinos Río Paraná SA", "Transportes El Ceibo", "Soja", "Marcos Juárez", "", "R0005-00010020", 43120, None),
]
for (pat, acop, chofer, dni, cli, tra, prod, proc, dest, rem, ent, sal) in pesadas:
    p = llamar("POST", "/api/pesadas/entrada", {"empresaId": e1, "patenteChasis": pat, "patenteAcoplado": acop, "conductor": chofer,
                                                "documento": dni, "tipoDocumento": "DNI", "nacionalidad": "Argentina", "cliente": cli,
                                                "transporte": tra, "producto": prod, "procedencia": proc, "destino": dest or None,
                                                "comprobante": rem or None, "pesoEntrada": ent, "claveManual": None}, sup)
    if sal:
        llamar("POST", f"/api/pesadas/{p['id']}/salida", {"pesoSalida": sal}, sup)

# Una de aduana, cerrada.
p = llamar("POST", "/api/pesadas/entrada", {"empresaId": e1, "patenteChasis": "AH222ST", "conductor": "Jorge Medina", "documento": "29888777",
                                            "tipoDocumento": "DNI", "nacionalidad": "Argentina", "cliente": "Molinos Río Paraná SA",
                                            "transporte": "Logística Paraná SRL", "producto": "Soja", "procedencia": "San Lorenzo", "destino": "Puerto Rosario",
                                            "pesoEntrada": 18450, "tipoOperacion": "Aduana",
                                            "aduana": {"escenario": "Carga Contenedor Lleno", "exportador": "Granos del Litoral SA",
                                                       "operacionId": "26001EC01000123K", "ata": "Logística Paraná SRL",
                                                       "destinacionAduanera": "EC01", "contenedorId": "MSCU4567890", "contenedorTara": 3800}}, sup)
llamar("POST", f"/api/pesadas/{p['id']}/salida", {"pesoSalida": 46870}, sup)

# Una corrección, para que haya historial.
lista = llamar("GET", "/api/pesadas", token=sup)
cerrada = next(x for x in lista if x["patenteChasis"] == "AF987GH")
llamar("POST", f"/api/pesadas/{cerrada['id']}/corregir", {"motivo": "Remito mal tipeado", "datos": {"comprobante": "R0001-00004576"}}, sup)
print("ok", e1, e2)
