-- Ejecutar después de 001 y 002. Datos de prueba revertidos al finalizar.
-- Las secuencias de identidad pueden avanzar aun cuando se use ROLLBACK.
BEGIN;
DO $$
DECLARE v_ingreso integer; v_reserva integer; v_cliente integer; v_rechazo boolean;
BEGIN
    SELECT min(id) INTO v_ingreso FROM user_07.mp_ingresos;
    SELECT min(id) INTO v_reserva FROM user_07.mp_reservas WHERE estado='RESERVADO';
    SELECT cliente_id INTO v_cliente FROM user_07.mp_reservas WHERE id=v_reserva;
    IF v_ingreso IS NULL OR v_reserva IS NULL THEN RAISE EXCEPTION 'Ejecute primero los datos de demostración.'; END IF;

    v_rechazo := false;
    BEGIN UPDATE user_07.mp_ingresos SET cantidad=cantidad+1 WHERE id=v_ingreso;
    EXCEPTION WHEN raise_exception THEN v_rechazo := true; END;
    IF NOT v_rechazo THEN RAISE EXCEPTION 'FALLO: se modificó un ingreso'; END IF;
    RAISE NOTICE 'OK: ingreso inmutable';

    v_rechazo := false;
    BEGIN DELETE FROM user_07.mp_reservas WHERE id=v_reserva;
    EXCEPTION WHEN raise_exception THEN v_rechazo := true; END;
    IF NOT v_rechazo THEN RAISE EXCEPTION 'FALLO: se borró una reserva'; END IF;
    RAISE NOTICE 'OK: reserva no eliminable';

    v_rechazo := false;
    BEGIN UPDATE user_07.mp_reservas SET cantidad=cantidad+1 WHERE id=v_reserva;
    EXCEPTION WHEN raise_exception THEN v_rechazo := true; END;
    IF NOT v_rechazo THEN RAISE EXCEPTION 'FALLO: se modificó la cantidad reservada'; END IF;
    RAISE NOTICE 'OK: solamente puede cambiar el estado';

    v_rechazo := false;
    BEGIN DELETE FROM user_07.mp_clientes WHERE id=v_cliente;
    EXCEPTION WHEN foreign_key_violation OR restrict_violation THEN v_rechazo := true; END;
    IF NOT v_rechazo THEN RAISE EXCEPTION 'FALLO: se borró un cliente con reservas'; END IF;
    RAISE NOTICE 'OK: referencias protegidas';

    v_rechazo := false;
    BEGIN UPDATE user_07.mp_productos SET atributos='[]';
    EXCEPTION WHEN check_violation THEN v_rechazo := true; END;
    IF NOT v_rechazo THEN RAISE EXCEPTION 'FALLO: se aceptó JSON que no es objeto'; END IF;
    RAISE NOTICE 'OK: atributos deben ser objeto JSON';
END; $$;
SELECT 'Existencias sin saldos negativos' AS prueba, bool_and(disponible>=0 AND reservada>=0) AS resultado FROM user_07.mp_v_productos_existencias;
ROLLBACK;
