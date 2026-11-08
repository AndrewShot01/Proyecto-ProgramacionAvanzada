-- Datos ficticios. Reejecutable; no duplica ingresos ni reservas.
BEGIN;
INSERT INTO user_07.mp_sucursales(nombre,direccion,telefono) VALUES
('Sucursal Centro','San José, avenida central','2222-0101'),
('Sucursal Heredia','Heredia, centro','2260-0102'),
('Sucursal Cartago','Cartago, centro','2551-0103') ON CONFLICT DO NOTHING;
INSERT INTO user_07.mp_clientes(identificacion,nombre,correo,telefono) VALUES
('DEMO-001','Ana Mora','ana@example.test','8888-0101'),
('DEMO-002','Luis Solís','luis@example.test','8888-0102'),
('DEMO-003','María Rojas','maria@example.test','8888-0103') ON CONFLICT DO NOTHING;
INSERT INTO user_07.mp_productos(codigo,nombre,precio,atributos,maximo_reserva,horas_reserva,existencia_minima) VALUES
('ZAP-001','Zapatos de mujer',25900,'{"talla":38,"color":"negro","material":"cuero"}',3,48,2),
('CRO-001','Crocs de niño',15900,'{"talla":28,"color":"azul"}',1,6,1),
('CAF-001','Café de la casa',4500,'{"pesoGramos":500,"tueste":"medio","origen":"Tarrazú"}',5,24,3)
ON CONFLICT DO NOTHING;
INSERT INTO user_07.mp_inventarios(sucursal_id,producto_id)
SELECT s.id,p.id FROM user_07.mp_sucursales s CROSS JOIN user_07.mp_productos p
WHERE s.nombre IN ('Sucursal Centro','Sucursal Heredia','Sucursal Cartago') AND p.codigo IN ('ZAP-001','CRO-001','CAF-001')
ON CONFLICT DO NOTHING;
INSERT INTO user_07.mp_ingresos(inventario_id,cantidad,referencia)
SELECT i.id,20,'Carga inicial de demostración' FROM user_07.mp_inventarios i
JOIN user_07.mp_sucursales s ON s.id=i.sucursal_id JOIN user_07.mp_productos p ON p.id=i.producto_id
WHERE s.nombre IN ('Sucursal Centro','Sucursal Heredia','Sucursal Cartago') AND p.codigo IN ('ZAP-001','CRO-001','CAF-001')
AND NOT EXISTS (SELECT 1 FROM user_07.mp_ingresos e WHERE e.inventario_id=i.id AND e.referencia='Carga inicial de demostración');
INSERT INTO user_07.mp_reservas(inventario_id,cliente_id,cantidad)
SELECT i.id,c.id,2 FROM user_07.mp_inventarios i
JOIN user_07.mp_sucursales s ON s.id=i.sucursal_id JOIN user_07.mp_productos p ON p.id=i.producto_id
CROSS JOIN user_07.mp_clientes c
WHERE s.nombre='Sucursal Centro' AND p.codigo='ZAP-001' AND c.identificacion='DEMO-001'
AND NOT EXISTS (SELECT 1 FROM user_07.mp_reservas r WHERE r.inventario_id=i.id AND r.cliente_id=c.id);
INSERT INTO user_07.mp_reservas(inventario_id,cliente_id,cantidad)
SELECT i.id,c.id,1 FROM user_07.mp_inventarios i
JOIN user_07.mp_sucursales s ON s.id=i.sucursal_id JOIN user_07.mp_productos p ON p.id=i.producto_id
CROSS JOIN user_07.mp_clientes c
WHERE s.nombre='Sucursal Heredia' AND p.codigo='CAF-001' AND c.identificacion='DEMO-002'
AND NOT EXISTS (SELECT 1 FROM user_07.mp_reservas r WHERE r.inventario_id=i.id AND r.cliente_id=c.id);
UPDATE user_07.mp_reservas SET estado='COMPRADO'
WHERE estado='RESERVADO' AND fecha_vencimiento > statement_timestamp()
AND cliente_id=(SELECT id FROM user_07.mp_clientes WHERE identificacion='DEMO-002')
AND inventario_id IN (SELECT i.id FROM user_07.mp_inventarios i JOIN user_07.mp_productos p ON p.id=i.producto_id WHERE p.codigo='CAF-001');
COMMIT;
