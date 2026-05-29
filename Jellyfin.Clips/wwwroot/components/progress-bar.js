(function () {
    'use strict';

    class ProgressBar extends HTMLElement {
        constructor() {
            super();
            this._progress = 0;
        }

        set progress(value) {
            this._progress = Math.max(0, Math.min(100, value));
            this.update();
        }

        get progress() {
            return this._progress;
        }

        connectedCallback() {
            this.render();
        }

        render() {
            this.innerHTML = `
                <div class="progress-bar">
                    <div class="progress-fill" style="width: ${this._progress}%"></div>
                </div>
            `;
        }

        update() {
            const fill = this.querySelector('.progress-fill');
            if (fill) {
                fill.style.width = `${this._progress}%`;
            }
        }
    }

    customElements.define('clip-progress-bar', ProgressBar);
})();
