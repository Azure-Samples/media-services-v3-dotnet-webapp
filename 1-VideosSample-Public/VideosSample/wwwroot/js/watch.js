function playVideo(data) {
    const title = document.getElementById('title');
    title.innerHTML = data.title;

    var options = {
        autoplay: true,
        controls: true,
        width: "1000",
        height: "600",
        logo: { enabled: false }
    };
    var player = amp('player', options);
    player.src([{ src: data.locator, type: "application/vnd.ms-sstr+xml" },]);
}

const params = new URLSearchParams(window.location.search);
const videoId = params.get('video');

fetch('/videos/' + videoId)
    .then(response => response.json())
    .then(data => playVideo(data));
