(function () {
    'use strict';

    const API_BASE = '/Plugins/Clips';
    const PAGE_SIZE = 20;

    let clips = [];
    let currentIndex = 0;
    let isLoading = false;
    let hasMore = true;
    let nextCursor = null;
    let currentGenre = '';
    let interactionStartTime = 0;
    let viewedClipIds = new Set();

    const feed = document.getElementById('clips-feed');
    const loading = document.getElementById('loading-indicator');
    const empty = document.getElementById('clips-empty');
    const modal = document.getElementById('video-modal');
    const modalVideo = document.getElementById('modal-video');
    const genreFilter = document.getElementById('genre-filter');

    async function getHeaders() {
        const token = await getAuthToken();
        return {
            'Content-Type': 'application/json',
            'Authorization': `MediaBrowser Token="${token}"`
        };
    }

    function getAuthToken() {
        return new Promise((resolve) => {
            if (typeof ApiClient !== 'undefined' && ApiClient.accessToken) {
                resolve(ApiClient.accessToken());
            } else {
                const stored = localStorage.getItem('jellyfin_token') || '';
                resolve(stored);
            }
        });
    }

    async function loadFeed(append) {
        if (isLoading) return;
        isLoading = true;
        loading.style.display = 'flex';

        try {
            const headers = await getHeaders();
            const params = new URLSearchParams({ count: PAGE_SIZE });
            if (append && nextCursor) params.set('cursor', nextCursor);
            if (currentGenre) params.set('genre', currentGenre);

            const resp = await fetch(`${API_BASE}/Feed?${params}`, { headers });
            if (!resp.ok) throw new Error(`HTTP ${resp.status}`);

            const data = await resp.json();
            if (append) {
                clips = clips.concat(data.clips);
            } else {
                clips = data.clips;
            }
            nextCursor = data.nextCursor;
            hasMore = data.clips.length === PAGE_SIZE;

            renderClips();
        } catch (err) {
            console.error('Failed to load feed:', err);
        } finally {
            isLoading = false;
            loading.style.display = 'none';
        }
    }

    function renderClips() {
        if (clips.length === 0) {
            feed.style.display = 'none';
            empty.style.display = 'flex';
            return;
        }

        feed.style.display = 'block';
        empty.style.display = 'none';

        const existing = feed.querySelectorAll('.clip-card');
        const existingIds = new Set(Array.from(existing).map(el => el.dataset.clipId));

        clips.forEach((clip, index) => {
            if (existingIds.has(clip.id)) return;

            const card = createClipCard(clip, index);
            feed.appendChild(card);
        });
    }

    function createClipCard(clip, index) {
        const card = document.createElement('div');
        card.className = 'clip-card';
        card.dataset.clipId = clip.id;
        card.dataset.index = index;

        const duration = formatDuration(clip.durationSeconds);
        const startTime = formatTime(clip.startTimeTicks / 10000000);

        card.innerHTML = `
            <div class="clip-card-inner">
                <img class="clip-thumbnail" src="${clip.thumbnailUrl}" alt="${clip.sourceItemName}" loading="lazy">
                <div class="clip-overlay">
                    <div class="clip-title">${clip.sourceItemName}</div>
                    <div class="clip-source">原片 · ${startTime}</div>
                    <div class="clip-meta">
                        <span class="genre-tag">${clip.genre || '未分类'}</span>
                        <span>${duration}</span>
                        <span>${clip.likeCount} 赞</span>
                    </div>
                </div>
                <div class="clip-side-actions">
                    <button class="side-action-btn ${clip.hasLiked ? 'liked' : ''}" data-action="like" data-clip-id="${clip.id}">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
                        </svg>
                        <span>${clip.likeCount}</span>
                    </button>
                    <button class="side-action-btn" data-action="original" data-clip-id="${clip.id}">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <polygon points="5 3 19 12 5 21 5 3"/>
                        </svg>
                        <span>原片</span>
                    </button>
                </div>
                <div class="play-indicator">
                    <svg viewBox="0 0 24 24" fill="white">
                        <polygon points="5 3 19 12 5 21 5 3"/>
                    </svg>
                </div>
            </div>
        `;

        card.addEventListener('click', (e) => {
            if (e.target.closest('.side-action-btn')) return;
            openClipModal(clip);
        });

        card.querySelectorAll('.side-action-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                handleSideAction(btn.dataset.action, btn.dataset.clipId);
            });
        });

        return card;
    }

    function openClipModal(clip) {
        modal.style.display = 'flex';
        modalVideo.src = clip.streamUrl;
        document.getElementById('video-title').textContent = clip.sourceItemName;
        document.getElementById('video-source').textContent = `原片 · ${formatTime(clip.startTimeTicks / 10000000)}`;
        document.getElementById('like-count').textContent = clip.likeCount;

        const likeBtn = document.getElementById('btn-like');
        if (clip.hasLiked) {
            likeBtn.classList.add('liked');
        } else {
            likeBtn.classList.remove('liked');
        }

        interactionStartTime = Date.now();
        viewedClipIds.add(clip.id);

        document.getElementById('btn-original').onclick = () => {
            reportInteraction(clip.id, 'clickThrough');
            const url = `/web/index.html#!/details?id=${clip.sourceItemId}`;
            window.open(url, '_blank');
        };

        document.getElementById('btn-like').onclick = async () => {
            const resp = await fetch(`${API_BASE}/Like/${clip.id}`, {
                method: 'POST',
                headers: await getHeaders()
            });
            const data = await resp.json();
            if (data.liked) {
                likeBtn.classList.add('liked');
                clip.hasLiked = true;
                clip.likeCount++;
            } else {
                likeBtn.classList.remove('liked');
                clip.hasLiked = false;
                clip.likeCount--;
            }
            document.getElementById('like-count').textContent = clip.likeCount;
        };

        modalVideo.ontimeupdate = () => {
            if (modalVideo.duration) {
                const pct = (modalVideo.currentTime / modalVideo.duration) * 100;
                document.getElementById('progress-fill').style.width = pct + '%';
            }
        };
    }

    function closeClipModal() {
        const clipId = modal.dataset.currentClipId;
        if (clipId) {
            const dwellTime = Date.now() - interactionStartTime;
            const completionRate = modalVideo.duration
                ? modalVideo.currentTime / modalVideo.duration
                : 0;
            reportInteraction(clipId, dwellTime < 3000 ? 'skip' : 'view', dwellTime, completionRate);
        }
        modal.style.display = 'none';
        modalVideo.pause();
        modalVideo.src = '';
    }

    async function handleSideAction(action, clipId) {
        if (action === 'like') {
            const headers = await getHeaders();
            const resp = await fetch(`${API_BASE}/Like/${clipId}`, {
                method: 'POST',
                headers
            });
            const data = await resp.json();
            const btn = document.querySelector(`[data-action="like"][data-clip-id="${clipId}"]`);
            if (data.liked) {
                btn.classList.add('liked');
            } else {
                btn.classList.remove('liked');
            }
        } else if (action === 'original') {
            const clip = clips.find(c => c.id === clipId);
            if (clip) {
                reportInteraction(clipId, 'clickThrough');
                window.open(`/web/index.html#!/details?id=${clip.sourceItemId}`, '_blank');
            }
        }
    }

    async function reportInteraction(clipId, type, dwellTimeMs, completionRate) {
        try {
            const headers = await getHeaders();
            await fetch(`${API_BASE}/Interaction`, {
                method: 'POST',
                headers,
                body: JSON.stringify({
                    clipId,
                    type: mapInteractionType(type),
                    dwellTimeMs: dwellTimeMs || 0,
                    completionRate: completionRate || 0
                })
            });
        } catch (err) {
            console.error('Failed to report interaction:', err);
        }
    }

    function mapInteractionType(type) {
        const map = {
            'view': 0,
            'like': 1,
            'dislike': 2,
            'skip': 3,
            'rewatch': 4,
            'clickThrough': 5,
            'share': 6
        };
        return map[type] ?? 0;
    }

    function formatDuration(seconds) {
        const m = Math.floor(seconds / 60);
        const s = seconds % 60;
        return `${m}:${s.toString().padStart(2, '0')}`;
    }

    function formatTime(seconds) {
        const m = Math.floor(seconds / 60);
        const s = Math.floor(seconds % 60);
        return `${m}:${s.toString().padStart(2, '0')}`;
    }

    let scrollTimeout;
    feed.addEventListener('scroll', () => {
        clearTimeout(scrollTimeout);
        scrollTimeout = setTimeout(() => {
            const scrollTop = feed.scrollTop;
            const cardHeight = feed.clientHeight;
            const newIndex = Math.round(scrollTop / cardHeight);

            if (newIndex !== currentIndex) {
                currentIndex = newIndex;
            }

            if (hasMore && scrollTop + cardHeight * 2 >= feed.scrollHeight) {
                loadFeed(true);
            }
        }, 100);
    });

    document.getElementById('modal-close').addEventListener('click', closeClipModal);
    modal.addEventListener('click', (e) => {
        if (e.target === modal) closeClipModal();
    });

    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && modal.style.display === 'flex') {
            closeClipModal();
        }
    });

    feed.addEventListener('dblclick', (e) => {
        const card = e.target.closest('.clip-card');
        if (!card) return;
        const clipId = card.dataset.clipId;
        handleSideAction('like', clipId);
        showLikeAnimation(card);
    });

    function showLikeAnimation(container) {
        const anim = document.createElement('div');
        anim.className = 'like-animation';
        anim.innerHTML = `<svg viewBox="0 0 24 24"><path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/></svg>`;
        container.appendChild(anim);
        setTimeout(() => anim.remove(), 1000);
    }

    document.getElementById('btn-manual-generate')?.addEventListener('click', async () => {
        const btn = document.getElementById('btn-manual-generate');
        btn.textContent = '生成中...';
        btn.disabled = true;
        try {
            const headers = await getHeaders();
            await fetch(`${API_BASE}/Admin/Generate`, {
                method: 'POST',
                headers,
                body: JSON.stringify({ forceRegenerate: false })
            });
            btn.textContent = '已触发生成';
            setTimeout(() => loadFeed(false), 3000);
        } catch (err) {
            btn.textContent = '生成失败，请重试';
            btn.disabled = false;
        }
    });

    loadFeed(false);
})();
