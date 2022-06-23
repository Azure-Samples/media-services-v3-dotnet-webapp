function customCreateElement(tag, attributes, text) {
    var e = document.createElement(tag);

    for (var a in attributes) {
        e.setAttribute(a, attributes[a]);
    }

    if (text) {
        e.innerHTML = text;
    }

    return e;
}

function playVideo(data) {
    const videoContainer = document.getElementById('videos');

    data.forEach(video => {
        let videoCard = customCreateElement('div', { class: 'card' });
        videoCard.appendChild(customCreateElement('img', { class: 'thumbnail', src: video.thumbnail }));
        videoCard.appendChild(customCreateElement('div', { class: 'cardtitle' }, video.title));

        let videoLink = customCreateElement('a', { href: '/watch.html?video=' + video.id });
        videoLink.appendChild(videoCard);

        videoContainer.appendChild(videoLink);
    });
}

fetch('/videos')
    .then(response => response.json())
    .then(data => playVideo(data));
