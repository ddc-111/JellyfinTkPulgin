(function () {
    'use strict';

    class VideoCard extends HTMLElement {
        constructor() {
            super();
            this._clip = null;
            this._isPlaying = false;
        }

        set clip(data) {
            this._clip = data;
            this.render();
        }

        get clip() {
            return this._clip;
        }

        render() {
            if (!this._clip) return;

            const clip = this._clip;
            this.innerHTML = `
                <div class="clip-card-inner" data-clip-id="${clip.id}">
                    <img class="clip-thumbnail"
                         src="${clip.thumbnailUrl}"
                         alt="${clip.sourceItemName}"
                         loading="lazy">
                    <div class="clip-overlay">
                        <div class="clip-title">${this.escapeHtml(clip.sourceItemName)}</div>
                        <div class="clip-source">${this.formatTime(clip.startTimeTicks / 10000000)}</div>
                        <div class="clip-meta">
                            ${clip.genre ? `<span class="genre-tag">${this.escapeHtml(clip.genre)}</span>` : ''}
                            <span>${this.formatDuration(clip.durationSeconds)}</span>
                        </div>
                    </div>
                    <div class="play-indicator">
                        <svg viewBox="0 0 24 24" fill="white"><polygon points="5 3 19 12 5 21 5 3"/></svg>
                    </div>
                </div>
            `;
        }

        escapeHtml(str) {
            const div = document.createElement('div');
            div.textContent = str;
            return div.innerHTML;
        }

        formatDuration(seconds) {
            const m = Math.floor(seconds / 60);
            const s = seconds % 60;
            return `${m}:${s.toString().padStart(2, '0')}`;
        }

        formatTime(seconds) {
            const m = Math.floor(seconds / 60);
            const s = Math.floor(seconds % 60);
            return `${m}:${s.toString().padStart(2, '0')}`;
        }
    }

    customElements.define('clip-video-card', VideoCard);
})();
