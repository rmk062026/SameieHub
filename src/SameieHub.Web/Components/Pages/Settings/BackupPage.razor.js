// Hands a backup to the browser as a download. The file arrives as a stream over the existing connection and is turned into a
// temporary address that only exists in this browser tab, so no address on the server ever points at a database file.
export async function downloadFromStream(fileName, streamReference) {
    const buffer = await streamReference.arrayBuffer();
    const blob = new Blob([buffer], { type: 'application/octet-stream' });
    const address = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = address;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(address);
}
