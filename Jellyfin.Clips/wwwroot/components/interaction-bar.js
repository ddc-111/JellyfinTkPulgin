(function () {
    'use strict';

    class InteractionBar extends HTMLElement {
        constructor() {
            super();
            this._clipId = '';
            this._likeCount = 0;
            this._hasLiked = false;
        }

        set data({ clipId, likeCount, hasLiked }) {
            this._clipId = clipId;
            this._likeCount = likeCount;
            this._hasLiked = hasLiked;
            this.render();
        }

        render() {
            this.innerHTML = `
                <div class="interaction-bar">
                    <button class="interaction-btn ${this._hasLiked ? 'liked' : ''}"
                            data-action="like" data-clip-id="${this._clipId}">
                        <svg viewBox="0 0 24 24" fill="${this._hasLiked ? 'var(--clips-accent)' : 'none'}"
                             stroke="${this._hasLiked ? 'var(--clips-accent)' : 'currentColor'}" stroke-width="2">
                            <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
                        </svg>
                        <span>${this._likeCount}</span>
                    </button>
                    <button class="interaction-btn" data-action="original" data-clip-id="${this._clipId}">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <polygon points="5 3 19 12 5 21 5 3"/>
                        </svg>
                        <span>原片</span>
                    </button>
                    <button class="interaction-btn" data-action="share" data-clip-id="${this._clipId}">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <circle cx="18" cy="5" r="3"/><circle cx="6" cy="12" r="3"/><circle cx="18" cy="19" r="3"/>
                            <line x1="8.59" y1="13.51" x2="15.42" y2="17.49"/>
                            <line x1="15.41" y1="6.51" x2="8.59" y2="10.49"/>
                        </svg>
                    </button>
                </div>
            `;
        }
    }

    customElements.define('clip-interaction-bar', InteractionBar);
})();
