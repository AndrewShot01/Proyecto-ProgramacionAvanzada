$(function () {
    // jQuery: vista previa y validación de atributos sin interpretar HTML del usuario.
    const editor = $('#Atributos');
    function previewJson() {
        if (!editor.length) return;
        try {
            const value = JSON.parse(editor.val());
            if (!value || Array.isArray(value) || typeof value !== 'object') throw new Error();
            $('#json-preview').text(JSON.stringify(value, null, 2));
            $('#json-status').text('JSON válido').removeClass('invalid');
        } catch (_) {
            $('#json-preview').text('');
            $('#json-status').text('Escribe un objeto JSON válido.').addClass('invalid');
        }
    }
    editor.on('input', previewJson);
    previewJson();
    $('form[data-maintenance]').on('submit', function () {
        if ($(this).valid && !$(this).valid()) return;
        $(this).find('button[type=submit]').prop('disabled', true).text('Guardando…');
    });
});
