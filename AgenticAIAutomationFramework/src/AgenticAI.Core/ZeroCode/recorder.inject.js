(function(){
    function getAttrSelector(el) {
        if (!el || !el.getAttribute) return null;
        var attrs = ['data-test-id','data-testid','data-test','name','aria-label','placeholder','title','id'];
        for (var i = 0; i < attrs.length; i++) {
            var a = attrs[i];
            try {
                var v = el.getAttribute(a);
                if (v) {
                    v = v.trim();
                    if (v.length > 0) return '[' + a + "='" + v.replace(/'/g, "\\'") + "']";
                }
            } catch(e){}
        }
        return null;
    }

    function getSimpleSelector(el) {
        if (!el) return '';
        var byAttr = getAttrSelector(el);
        if (byAttr) return byAttr;
        if (el.id) return '#' + el.id;
        var sel = el.tagName.toLowerCase();
        if (el.classList && el.classList.length > 0) {
            sel += '.' + Array.from(el.classList).filter(c=>c.trim()).join('.');
        }
        return sel;
    }

    function getXPath(element) {
        if (!element) return '';
        if (element.id) {
            return "//*[@id='" + element.id + "']";
        }
        var parts = [];
        while (element && element.nodeType === Node.ELEMENT_NODE) {
            var nb = 0;
            var sib = element.previousSibling;
            while (sib) {
                if (sib.nodeType === Node.ELEMENT_NODE && sib.nodeName === element.nodeName) nb++;
                sib = sib.previousSibling;
            }
            var prefix = element.prefix ? element.prefix + ':' : '';
            var nth = (nb ? '[' + (nb+1) + ']' : '');
            parts.unshift(prefix + element.localName + nth);
            element = element.parentNode;
        }
        return parts.length ? '/' + parts.join('/') : '';
    }

    function sendAction(obj){
        try{
            if (window.__recordAction){
                window.__recordAction(JSON.stringify(obj));
            }
        }catch(e){ console.warn('sendAction error', e); }
    }

    document.addEventListener('click', function(e){
        try{
            var el = e.target;
            var selector = getSimpleSelector(el);
            if (!selector || selector === ''){
                el = el.closest('button, a, input, [role=button]') || el;
                selector = getSimpleSelector(el);
            }
            var xpath = '';
            try{ xpath = getXPath(el); }catch(e){}
            sendAction({ actionType: 'Click', css: selector || el.tagName.toLowerCase(), xpath: xpath, value: '', description: 'Click on ' + (selector || el.tagName.toLowerCase()) });
        }catch(ex){ console.log('record click error', ex); }
    }, true);

    document.addEventListener('input', function(e){
        try{
            var el = e.target;
            var selector = getSimpleSelector(el);
            var value = el.value || '';
            var xpath = '';
            try{ xpath = getXPath(el); }catch(e){}
            sendAction({ actionType: 'Type', css: selector || el.tagName.toLowerCase(), xpath: xpath, value: value, description: 'Type into ' + (selector || el.tagName.toLowerCase()) });
        }catch(ex){ console.log('record input error', ex); }
    }, true);
})();
