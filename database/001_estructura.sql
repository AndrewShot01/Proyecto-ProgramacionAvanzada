-- Ejecutar una sola vez en advanced_programming, con permiso CREATE en user_07.
-- No elimina ni modifica las tablas PRQ del curso. Todo queda en una transacción.
BEGIN;
-- El profesor ya proporciona este esquema. No solicitar CREATE sobre la base.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname='user_07') THEN
        RAISE EXCEPTION 'El esquema user_07 debe existir antes de instalar el proyecto.';
    END IF;
END; $$;

CREATE TABLE user_07.mp_sucursales (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre varchar(100) NOT NULL CHECK (length(btrim(nombre)) > 0),
    direccion varchar(250) NOT NULL CHECK (length(btrim(direccion)) > 0),
    telefono varchar(25) NOT NULL CHECK (length(btrim(telefono)) > 0)
);
CREATE UNIQUE INDEX uq_mp_sucursales_nombre ON user_07.mp_sucursales (lower(btrim(nombre)));
CREATE TABLE user_07.mp_clientes (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    identificacion varchar(30) NOT NULL CHECK (length(btrim(identificacion)) > 0),
    nombre varchar(120) NOT NULL CHECK (length(btrim(nombre)) > 0),
    correo varchar(150) NOT NULL CHECK (correo ~ '^[^[:space:]@]+@[^[:space:]@]+\.[^[:space:]@]+$'),
    telefono varchar(25) NOT NULL CHECK (length(btrim(telefono)) > 0)
);
CREATE UNIQUE INDEX uq_mp_clientes_identificacion ON user_07.mp_clientes (lower(btrim(identificacion)));
CREATE UNIQUE INDEX uq_mp_clientes_correo ON user_07.mp_clientes (lower(btrim(correo)));
CREATE TABLE user_07.mp_productos (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    codigo varchar(30) NOT NULL CHECK (length(btrim(codigo)) > 0),
    nombre varchar(120) NOT NULL CHECK (length(btrim(nombre)) > 0),
    precio numeric(12,2) NOT NULL CHECK (precio > 0),
    atributos jsonb NOT NULL DEFAULT '{}'::jsonb CHECK (jsonb_typeof(atributos) = 'object'),
    maximo_reserva integer NOT NULL CHECK (maximo_reserva > 0),
    horas_reserva numeric(12,6) NOT NULL CHECK (horas_reserva > 0 AND horas_reserva <= 8760),
    existencia_minima integer NOT NULL DEFAULT 0 CHECK (existencia_minima >= 0)
);
CREATE UNIQUE INDEX uq_mp_productos_codigo ON user_07.mp_productos (lower(btrim(codigo)));
CREATE TABLE user_07.mp_inventarios (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    sucursal_id integer NOT NULL REFERENCES user_07.mp_sucursales(id) ON DELETE RESTRICT,
    producto_id integer NOT NULL REFERENCES user_07.mp_productos(id) ON DELETE RESTRICT,
    UNIQUE (sucursal_id, producto_id)
);
CREATE TABLE user_07.mp_ingresos (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    inventario_id integer NOT NULL REFERENCES user_07.mp_inventarios(id) ON DELETE RESTRICT,
    cantidad integer NOT NULL CHECK (cantidad > 0),
    fecha timestamptz NOT NULL DEFAULT statement_timestamp(),
    referencia varchar(100) NOT NULL CHECK (length(btrim(referencia)) > 0)
);
CREATE TABLE user_07.mp_reservas (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    inventario_id integer NOT NULL REFERENCES user_07.mp_inventarios(id) ON DELETE RESTRICT,
    cliente_id integer NOT NULL REFERENCES user_07.mp_clientes(id) ON DELETE RESTRICT,
    cantidad integer NOT NULL CHECK (cantidad > 0),
    precio_unitario numeric(12,2) NOT NULL CHECK (precio_unitario > 0),
    fecha_reserva timestamptz NOT NULL,
    fecha_vencimiento timestamptz NOT NULL,
    fecha_cierre timestamptz,
    estado varchar(10) NOT NULL DEFAULT 'RESERVADO' CHECK (estado IN ('RESERVADO','COMPRADO','CANCELADO')),
    CHECK (fecha_vencimiento > fecha_reserva),
    CHECK ((estado = 'RESERVADO' AND fecha_cierre IS NULL) OR (estado <> 'RESERVADO' AND fecha_cierre IS NOT NULL))
);
CREATE INDEX ix_mp_ingresos_inventario ON user_07.mp_ingresos(inventario_id);
CREATE INDEX ix_mp_reservas_inventario ON user_07.mp_reservas(inventario_id);
CREATE INDEX ix_mp_reservas_cliente_fecha ON user_07.mp_reservas(cliente_id, fecha_reserva DESC);
CREATE INDEX ix_mp_reservas_vencimiento ON user_07.mp_reservas(fecha_vencimiento) WHERE estado = 'RESERVADO';

-- Los saldos se derivan del historial, evitando actualizaciones directas de cantidades.
CREATE VIEW user_07.mp_v_productos_existencias AS
SELECT i.id AS inventario_id, s.id AS sucursal_id, s.nombre AS sucursal,
       p.id AS producto_id, p.codigo, p.nombre AS producto, p.precio, p.atributos,
       p.maximo_reserva, p.horas_reserva, p.existencia_minima,
       coalesce(e.cantidad,0) - coalesce(r.comprometidas,0) AS disponible,
       coalesce(r.reservadas,0) AS reservada,
       coalesce(r.compradas,0) AS vendida
FROM user_07.mp_inventarios i
JOIN user_07.mp_sucursales s ON s.id = i.sucursal_id
JOIN user_07.mp_productos p ON p.id = i.producto_id
LEFT JOIN (SELECT inventario_id, sum(cantidad) cantidad FROM user_07.mp_ingresos GROUP BY inventario_id) e ON e.inventario_id = i.id
LEFT JOIN (SELECT inventario_id,
    sum(cantidad) FILTER (WHERE estado IN ('RESERVADO','COMPRADO')) comprometidas,
    sum(cantidad) FILTER (WHERE estado = 'RESERVADO') reservadas,
    sum(cantidad) FILTER (WHERE estado = 'COMPRADO') compradas
    FROM user_07.mp_reservas GROUP BY inventario_id) r ON r.inventario_id = i.id;

CREATE VIEW user_07.mp_v_reservas_clientes AS
SELECT r.id, c.id cliente_id, c.identificacion, c.nombre cliente,
       s.id sucursal_id, s.nombre sucursal, p.id producto_id, p.codigo, p.nombre producto,
       r.cantidad, r.precio_unitario, r.cantidad * r.precio_unitario total,
       r.fecha_reserva, r.fecha_vencimiento, r.fecha_cierre, r.estado,
       r.estado = 'COMPRADO' AS compra_materializada
FROM user_07.mp_reservas r
JOIN user_07.mp_clientes c ON c.id = r.cliente_id
JOIN user_07.mp_inventarios i ON i.id = r.inventario_id
JOIN user_07.mp_sucursales s ON s.id = i.sucursal_id
JOIN user_07.mp_productos p ON p.id = i.producto_id;

CREATE FUNCTION user_07.mp_impedir_cambios() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'El historial de % no permite %.', TG_TABLE_NAME, TG_OP USING ERRCODE = 'P0001';
END; $$;
CREATE TRIGGER mp_ingresos_inmutables BEFORE UPDATE OR DELETE ON user_07.mp_ingresos FOR EACH ROW EXECUTE FUNCTION user_07.mp_impedir_cambios();
CREATE TRIGGER mp_ingresos_no_truncate BEFORE TRUNCATE ON user_07.mp_ingresos FOR EACH STATEMENT EXECUTE FUNCTION user_07.mp_impedir_cambios();
CREATE TRIGGER mp_inventarios_inmutables BEFORE UPDATE OR DELETE ON user_07.mp_inventarios FOR EACH ROW EXECUTE FUNCTION user_07.mp_impedir_cambios();
CREATE TRIGGER mp_inventarios_no_truncate BEFORE TRUNCATE ON user_07.mp_inventarios FOR EACH STATEMENT EXECUTE FUNCTION user_07.mp_impedir_cambios();
CREATE TRIGGER mp_reservas_no_truncate BEFORE TRUNCATE ON user_07.mp_reservas FOR EACH STATEMENT EXECUTE FUNCTION user_07.mp_impedir_cambios();

CREATE FUNCTION user_07.mp_validar_reserva() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE p user_07.mp_productos%ROWTYPE; disponible bigint; acumulado bigint;
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'Las reservas no se pueden borrar.';
    ELSIF TG_OP = 'UPDATE' THEN
        IF OLD.estado <> 'RESERVADO' THEN
            RAISE EXCEPTION 'Una reserva finalizada es inmutable.';
        END IF;
        IF (to_jsonb(NEW) - 'estado') IS DISTINCT FROM (to_jsonb(OLD) - 'estado') THEN
            RAISE EXCEPTION 'Solamente se puede actualizar el estado de una reserva.';
        END IF;
        IF NEW.estado NOT IN ('COMPRADO','CANCELADO') THEN
            RAISE EXCEPTION 'La transición de estado no es válida.';
        END IF;
        IF NEW.estado = 'CANCELADO' AND OLD.fecha_vencimiento > statement_timestamp() THEN
            RAISE EXCEPTION 'No se permite cancelar una reserva antes de su vencimiento.';
        END IF;
        IF NEW.estado = 'COMPRADO' AND OLD.fecha_vencimiento <= statement_timestamp() THEN
            RAISE EXCEPTION 'No se permite comprar una reserva vencida.';
        END IF;
        -- Mismo orden de bloqueo para inserciones y cambios de estado.
        PERFORM 1 FROM user_07.mp_productos p2 JOIN user_07.mp_inventarios i ON i.producto_id = p2.id
            WHERE i.id = OLD.inventario_id FOR UPDATE OF p2;
        NEW.fecha_cierre := statement_timestamp();
        RETURN NEW;
    END IF;
    IF NEW.estado <> 'RESERVADO' OR NEW.fecha_cierre IS NOT NULL THEN
        RAISE EXCEPTION 'La reserva debe iniciar en estado RESERVADO.';
    END IF;
    -- Serializa reservas del mismo producto, incluso entre sucursales.
    SELECT p2.* INTO p FROM user_07.mp_productos p2 JOIN user_07.mp_inventarios i ON i.producto_id = p2.id
        WHERE i.id = NEW.inventario_id FOR UPDATE OF p2;
    IF NOT FOUND THEN RAISE EXCEPTION 'Inventario inexistente.' USING ERRCODE = '23503'; END IF;
    SELECT coalesce(sum(r.cantidad),0) INTO acumulado
        FROM user_07.mp_reservas r JOIN user_07.mp_inventarios i ON i.id = r.inventario_id
        WHERE r.cliente_id = NEW.cliente_id AND i.producto_id = p.id AND r.estado = 'RESERVADO';
    IF NEW.cantidad <= 0 OR acumulado + NEW.cantidad > p.maximo_reserva THEN
        RAISE EXCEPTION 'Se excede el máximo reservado por cliente para este producto.';
    END IF;
    SELECT v.disponible INTO disponible FROM user_07.mp_v_productos_existencias v WHERE v.inventario_id = NEW.inventario_id;
    IF disponible - NEW.cantidad < p.existencia_minima THEN
        RAISE EXCEPTION 'Inventario insuficiente o existencia mínima comprometida.';
    END IF;
    NEW.fecha_reserva := statement_timestamp();
    NEW.fecha_vencimiento := NEW.fecha_reserva + p.horas_reserva * interval '1 hour';
    NEW.precio_unitario := p.precio;
    RETURN NEW;
END; $$;
CREATE TRIGGER mp_reservas_reglas BEFORE INSERT OR UPDATE OR DELETE ON user_07.mp_reservas FOR EACH ROW EXECUTE FUNCTION user_07.mp_validar_reserva();

CREATE FUNCTION user_07.mp_validar_limites_producto() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.existencia_minima > OLD.existencia_minima AND EXISTS (
        SELECT 1 FROM user_07.mp_v_productos_existencias WHERE producto_id = OLD.id AND disponible < NEW.existencia_minima
    ) THEN RAISE EXCEPTION 'La existencia mínima supera el inventario disponible de una sucursal.'; END IF;
    IF NEW.maximo_reserva < OLD.maximo_reserva AND EXISTS (
        SELECT 1 FROM user_07.mp_reservas r JOIN user_07.mp_inventarios i ON i.id = r.inventario_id
        WHERE i.producto_id = OLD.id AND r.estado = 'RESERVADO'
        GROUP BY r.cliente_id HAVING sum(r.cantidad) > NEW.maximo_reserva
    ) THEN RAISE EXCEPTION 'Hay clientes con reservas superiores al nuevo máximo.'; END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER mp_productos_limites BEFORE UPDATE ON user_07.mp_productos FOR EACH ROW EXECUTE FUNCTION user_07.mp_validar_limites_producto();
COMMIT;
