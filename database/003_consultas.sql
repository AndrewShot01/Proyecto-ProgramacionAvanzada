-- Consultas individuales del primer entregable.
SELECT * FROM user_07.mp_sucursales ORDER BY nombre;
SELECT id, identificacion, nombre, correo, telefono FROM user_07.mp_clientes WHERE nombre ILIKE '%ana%' ORDER BY nombre;
SELECT * FROM user_07.mp_productos WHERE nombre ILIKE '%café%' ORDER BY nombre;
SELECT * FROM user_07.mp_v_productos_existencias ORDER BY producto, sucursal;
SELECT * FROM user_07.mp_v_reservas_clientes ORDER BY fecha_reserva DESC, id DESC;
