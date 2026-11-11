-- Ampliación incremental. Conserva tablas, datos e historial del primer avance.
BEGIN;
ALTER TABLE user_07.mp_ingresos ADD COLUMN IF NOT EXISTS solicitud_id uuid;
ALTER TABLE user_07.mp_reservas ADD COLUMN IF NOT EXISTS solicitud_id uuid;
CREATE UNIQUE INDEX IF NOT EXISTS uq_mp_ingresos_solicitud ON user_07.mp_ingresos(solicitud_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_mp_reservas_solicitud ON user_07.mp_reservas(solicitud_id);

-- Solo cancela reservas ya vencidas según el reloj de PostgreSQL.
-- SKIP LOCKED evita que dos ejecuciones procesen la misma reserva.
CREATE OR REPLACE FUNCTION user_07.mp_cancelar_reservas_vencidas(limite integer DEFAULT 200)
RETURNS integer LANGUAGE plpgsql AS $$
DECLARE cambiadas integer;
BEGIN
    IF limite < 1 OR limite > 1000 THEN RAISE EXCEPTION 'Límite de lote inválido.'; END IF;
    WITH vencidas AS (
        SELECT id FROM user_07.mp_reservas
        WHERE estado='RESERVADO' AND fecha_vencimiento <= statement_timestamp()
        ORDER BY id FOR UPDATE SKIP LOCKED LIMIT limite
    )
    UPDATE user_07.mp_reservas r SET estado='CANCELADO'
    FROM vencidas v WHERE r.id=v.id;
    GET DIAGNOSTICS cambiadas = ROW_COUNT;
    RETURN cambiadas;
END; $$;
COMMIT;
