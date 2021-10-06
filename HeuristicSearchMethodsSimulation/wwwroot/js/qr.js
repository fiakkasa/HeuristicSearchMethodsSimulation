window.addEventListener('load', () => {
    new QRCode(
        document.getElementById('qrCode'),
        {
            text: document.getElementById('qrCodeData').getAttribute('data-url'),
            width: 200,
            height: 200
        }
    );
});