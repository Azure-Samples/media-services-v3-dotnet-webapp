//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

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

        let videoLink = customCreateElement('a', { href: '/watch.html?video=' + video.videoId });
        videoLink.appendChild(videoCard);

        videoContainer.appendChild(videoLink);
    });
}

function loadVideos() {
    getTokenPopup(tokenRequest)
        .then(response => {
            if (response) {
                try {
                    const headers = new Headers();
                    const bearer = `Bearer ${response.accessToken}`;

                    headers.append("Authorization", bearer);

                    const options = {
                        method: "GET",
                        headers: headers
                    };

                    fetch('/videos', options)
                        .then(response => response.json())
                        .then(data => playVideo(data));
                } catch (error) {
                    console.warn(error);
                }
            }
        });
}

function signIn() {
    myMSALObj.loginPopup(loginRequest)
        .then(selectAccount)
        .then(loadVideos)
        .catch(error => {
            console.error(error);
        });
}

function signOut() {

    const logoutRequest = {
        account: myMSALObj.getAccountByUsername(username)
    };

    myMSALObj.logout(logoutRequest);
}

loadVideos();
